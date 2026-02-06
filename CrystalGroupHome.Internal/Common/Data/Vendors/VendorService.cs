using Dapper;
using Microsoft.Extensions.Options;
using Microsoft.Data.SqlClient;
using System.Data;
using CrystalGroupHome.SharedRCL.Data.Parts;
using CrystalGroupHome.SharedRCL.Data;
using CrystalGroupHome.SharedRCL.Data.Vendor;

namespace CrystalGroupHome.Internal.Common.Data.Vendors
{
    public interface IVendorService
    {
        Task<List<VendorDTO_Base>> GetVendorsByNumbersAsync(IEnumerable<int> vendorNumbers);
        Task<List<VendorPartInfoDTO>> GetVendorInfoForPartsAsync(IEnumerable<string> partNumbers, bool includeInactiveParts = false, bool includeInactiveVendors = false);
        Task<List<PartPrimaryVendorDTO>> GetAllPartsWithPrimaryVendorAsync(bool includeInactiveParts = false, bool includeInactiveVendors = false);
        Task<List<PartPrimaryVendorDTO>> GetPartsWithPrimaryVendorByPartNum(IEnumerable<string> partNumbers, bool includeInactiveParts = false, bool includeInactiveVendors = false);
    }

    public class VendorService : IVendorService
    {
        private readonly string _kineticConnectionString;
        private readonly ILogger<VendorService> _logger;

        // Table Stuff
        private const string PartTable = "[KineticErp].[dbo].[Part]";
        private const string VendorTable = "[KineticErp].[Erp].[Vendor]";
        private const string VendPartTable = "[KineticErp].[Erp].[VendPart]";
        private const string VendorPPTable = "[KineticErp].[Erp].[VendorPP]";
        private const string VendCntTable = "[KineticErp].[Erp].[VendCnt]";

        public VendorService(IOptions<DatabaseOptions> dbOptions, ILogger<VendorService> logger)
        {
            _kineticConnectionString = dbOptions.Value.KineticErpConnection;
            _logger = logger;
        }

        private IDbConnection Connection => new SqlConnection(_kineticConnectionString);

        private const string VendorPartInfoBaseQuery = $@"
            ;WITH LatestVendPart AS (
                SELECT vp.*,
                       ROW_NUMBER() OVER (
                           PARTITION BY vp.Company, vp.PartNum, vp.VendorNum
                           ORDER BY vp.EffectiveDate DESC, vp.SysRevID DESC
                       ) AS rn
                FROM {VendPartTable} vp
            )
            SELECT
                p.Company,
                lvp.PartNum,
                lvp.VenPartNum,
                v.VendorNum,
                v.Name                                  AS VendorName,
                v.PurPoint                               AS DefaultPurPoint,
                COALESCE(ppPrim.EMailAddress,
                         ppAny.EMailAddress,
                         vPrim.EMailAddress,
                         vAny.EMailAddress,
                         NULLIF(LTRIM(RTRIM(v.EMailAddress)), '')
                )                                         AS EmailAddress,
                CASE
                    WHEN ppPrim.EMailAddress IS NOT NULL THEN 'Purchase Point Primary Contact'
                    WHEN ppAny.EMailAddress  IS NOT NULL THEN 'Purchase Point Contact'
                    WHEN vPrim.EMailAddress  IS NOT NULL THEN 'Vendor Primary Contact'
                    WHEN vAny.EMailAddress   IS NOT NULL THEN 'Vendor Contact'
                    WHEN NULLIF(LTRIM(RTRIM(v.EMailAddress)), '') IS NOT NULL THEN 'Vendor Header Email'
                    ELSE 'No email on file'
                END                                       AS EmailSource,
                COALESCE(ppPrim.Name, ppAny.Name, vPrim.Name, vAny.Name, v.Name) AS ContactName
            FROM LatestVendPart lvp
            JOIN {PartTable}  p ON p.Company   = lvp.Company
                            AND p.PartNum   = lvp.PartNum
            JOIN {VendorTable} v ON v.Company  = lvp.Company
                            AND v.VendorNum = lvp.VendorNum
            OUTER APPLY (
                SELECT TOP (1)
                       vc.ConNum,
                       vc.Name,
                       NULLIF(LTRIM(RTRIM(vc.EMailAddress)), '') AS EMailAddress
                FROM {VendorPPTable} pp
                JOIN {VendCntTable}  vc
                  ON vc.Company    = pp.Company
                 AND vc.VendorNum  = pp.VendorNum
                 AND vc.PurPoint   = pp.PurPoint
                 AND vc.ConNum     = pp.PrimPCon
                WHERE pp.Company   = v.Company
                  AND pp.VendorNum = v.VendorNum
                  AND pp.PurPoint  = v.PurPoint
                  AND ISNULL(vc.Inactive,0) = 0
                  AND NULLIF(LTRIM(RTRIM(vc.EMailAddress)),'') IS NOT NULL
            ) ppPrim
            OUTER APPLY (
                SELECT TOP (1)
                       vc.ConNum,
                       vc.Name,
                       NULLIF(LTRIM(RTRIM(vc.EMailAddress)), '') AS EMailAddress
                FROM {VendCntTable} vc
                WHERE vc.Company    = v.Company
                  AND vc.VendorNum  = v.VendorNum
                  AND vc.PurPoint   = v.PurPoint
                  AND ISNULL(vc.Inactive,0) = 0
                  AND NULLIF(LTRIM(RTRIM(vc.EMailAddress)),'') IS NOT NULL
                  AND (ppPrim.ConNum IS NULL OR vc.ConNum <> ppPrim.ConNum)
                ORDER BY vc.ConNum
            ) ppAny
            OUTER APPLY (
                SELECT TOP (1)
                       vc.ConNum,
                       vc.Name,
                       NULLIF(LTRIM(RTRIM(vc.EMailAddress)), '') AS EMailAddress
                FROM {VendCntTable} vc
                WHERE vc.Company    = v.Company
                  AND vc.VendorNum  = v.VendorNum
                  AND vc.ConNum     = v.PrimPCon
                  AND (vc.PurPoint IS NULL OR vc.PurPoint = '')
                  AND ISNULL(vc.Inactive,0) = 0
                  AND NULLIF(LTRIM(RTRIM(vc.EMailAddress)),'') IS NOT NULL
            ) vPrim
            OUTER APPLY (
                SELECT TOP (1)
                       vc.ConNum,
                       vc.Name,
                       NULLIF(LTRIM(RTRIM(vc.EMailAddress)), '') AS EMailAddress
                FROM {VendCntTable} vc
                WHERE vc.Company    = v.Company
                  AND vc.VendorNum  = v.VendorNum
                  AND (vc.PurPoint IS NULL OR vc.PurPoint = '')
                  AND ISNULL(vc.Inactive,0) = 0
                  AND NULLIF(LTRIM(RTRIM(vc.EMailAddress)),'') IS NOT NULL
                  AND (vPrim.ConNum IS NULL OR vc.ConNum <> vPrim.ConNum)
                ORDER BY CASE WHEN vc.ConNum = v.PrimPCon THEN 1 ELSE 0 END DESC,
                         vc.ConNum
            ) vAny";

        public async Task<List<VendorPartInfoDTO>> GetVendorInfoForPartsAsync(IEnumerable<string> partNumbers, bool includeInactiveParts = false, bool includeInactiveVendors = false)
        {
            if (partNumbers == null || !partNumbers.Any())
            {
                return new List<VendorPartInfoDTO>();
            }

            var whereClauses = new List<string>
            {
                "lvp.rn = 1",
                "v.GroupCode IN ('C','CS','G','MP','PO')",
                "lvp.PartNum IN @PartNumbers"
            };

            if (!includeInactiveParts)
            {
                whereClauses.Add("ISNULL(p.InActive,0) = 0");
            }

            if (!includeInactiveVendors)
            {
                whereClauses.Add("ISNULL(v.InActive,0) = 0");
            }

            var finalQuery = $"{VendorPartInfoBaseQuery} WHERE {string.Join(" AND ", whereClauses)};";

            try
            {
                using var conn = Connection;
                var result = await conn.QueryAsync<VendorPartInfoDTO>(finalQuery, new { PartNumbers = partNumbers.Distinct() });
                return result.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching vendor part info for PartNumbers {@PartNumbers}", partNumbers);
                throw;
            }
        }

        public async Task<List<VendorDTO_Base>> GetVendorsByNumbersAsync(IEnumerable<int> vendorNumbers)
        {
            if (vendorNumbers == null || !vendorNumbers.Any())
            {
                return new List<VendorDTO_Base>();
            }

            const string query = $@"
                SELECT
                    VendorNum,
                    Name AS VendorName,
                    EMailAddress,
                    InActive
                FROM {VendorTable}
                WHERE VendorNum IN @VendorNumbers";

            try
            {
                using var conn = Connection;
                var result = await conn.QueryAsync<VendorDTO_Base>(query, new { VendorNumbers = vendorNumbers.Distinct() });
                return result.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching vendors by numbers {@VendorNumbers}", vendorNumbers);
                throw;
            }
        }

        private async Task<List<PartPrimaryVendorDTO>> GetPartsWithPrimaryVendorInternalAsync(
            IEnumerable<string>? partNumbers = null, 
            bool includeInactiveParts = false, 
            bool includeInactiveVendors = false)
        {
            var queryBuilder = new System.Text.StringBuilder($@"
                WITH LatestVendPart AS (
                    SELECT 
                        vp.PartNum, 
                        vp.VendorNum,
                        vx.VendPartNum, 
                        vx.MfgPartNum, 
                        m.Name AS MfgName,
                        ROW_NUMBER() OVER (PARTITION BY vp.PartNum, vp.VendorNum ORDER BY vp.EffectiveDate DESC) AS rn
                    FROM Erp.VendPart vp
                    LEFT JOIN Erp.PartXRefVend vx ON vx.Company = vp.Company AND vx.PartNum = vp.PartNum AND vx.VendorNum = vp.VendorNum
                    LEFT JOIN Erp.Manufacturer m ON m.Company = vx.Company AND vx.MfgNum = m.MfgNum
                    WHERE vp.Company = 'CG'
                )
                SELECT
                    p.PartNum,
                    p.PartDescription,
                    CASE WHEN (p.Deprecated_c = 1 OR p.InActive = 1) THEN 1 ELSE 0 END AS PartInactive,
                    v.VendorNum,
                    v.Name AS VendorName,
                    v.InActive AS VendorInactive,
                    lvp.VendPartNum,
                    lvp.MfgPartNum,
                    lvp.MfgName
                FROM {PartTable} p 
                LEFT JOIN dbo.PartPlant pp ON p.Company = pp.Company AND p.PartNum = pp.PartNum
                LEFT JOIN {VendorTable} v ON v.Company = p.Company AND v.VendorNum = pp.VendorNum
                LEFT JOIN LatestVendPart lvp ON lvp.PartNum = p.PartNum AND lvp.VendorNum = pp.VendorNum AND lvp.rn = 1
            ");

            var whereClauses = new List<string>
            {
                "p.TypeCode = 'P'",
                @"p.CommodityCode_c NOT IN 
                ('CSP',
                 'CRS',
                 'DCL',
                 'DDD',
                 'DOC',
                 'ENG',
                 'FMW',
                 'FRM',
                 'HDW',
                 'LSR',
                 'OID',
                 'SCH',
                 'SLK',
                 'SMA',
                 'STL',
                 'TLD',
                 'TOL',
                 'TST',
                 'VMA',
                 'WIR',
                 'WTY')"
            };

            // Add part number filter if provided
            if (partNumbers != null && partNumbers.Any())
            {
                whereClauses.Add("p.PartNum IN @PartNumbers");
            }

            if (!includeInactiveParts)
            {
                whereClauses.Add("ISNULL(p.InActive,0) = 0");
                whereClauses.Add("ISNULL(p.Deprecated_c,0) = 0");
            }

            if (!includeInactiveVendors)
            {
                whereClauses.Add("ISNULL(v.InActive,0) = 0");
            }

            queryBuilder.Append($" WHERE {string.Join(" AND ", whereClauses)}");
            queryBuilder.Append(";");

            var finalQuery = queryBuilder.ToString();

            try
            {
                using var conn = Connection;
                var parameters = partNumbers?.Any() == true 
                    ? new { PartNumbers = partNumbers.Distinct() } 
                    : null;
                
                var results = await conn.QueryAsync<dynamic>(finalQuery, parameters);

                return results.Select(r => new PartPrimaryVendorDTO
                {
                    PartNum = r.PartNum,
                    PartDescription = r.PartDescription,
                    InActive = Convert.ToBoolean(r.PartInactive),
                    PrimaryVendor = r.VendorNum != null ? new VendorDTO_Base
                    {
                        VendorNum = r.VendorNum,
                        VendorName = r.VendorName,
                        InActive = Convert.ToBoolean(r.VendorInactive)
                    } : null,
                    VendorPartNum = r.VendPartNum,
                    MfgPartNum = r.MfgPartNum,
                    MfgName = r.MfgName
                }).ToList();
            }
            catch (Exception ex)
            {
                var errorMessage = partNumbers?.Any() == true 
                    ? "Error fetching parts with primary vendor for specific PartNumbers {@PartNumbers}"
                    : "Error fetching all parts with primary vendor";
                
                _logger.LogError(ex, errorMessage, partNumbers);
                throw;
            }
        }

        public async Task<List<PartPrimaryVendorDTO>> GetAllPartsWithPrimaryVendorAsync(bool includeInactiveParts = false, bool includeInactiveVendors = false)
        {
            return await GetPartsWithPrimaryVendorInternalAsync(
                partNumbers: null, 
                includeInactiveParts: includeInactiveParts, 
                includeInactiveVendors: includeInactiveVendors);
        }

        public async Task<List<PartPrimaryVendorDTO>> GetPartsWithPrimaryVendorByPartNum(IEnumerable<string> partNumbers, bool includeInactiveParts = false, bool includeInactiveVendors = false)
        {
            if (partNumbers == null || !partNumbers.Any())
            {
                return new List<PartPrimaryVendorDTO>();
            }

            var distinctPartNumbers = partNumbers.Distinct().ToList();
            
            // If we have a reasonable number of parts, process normally
            if (distinctPartNumbers.Count <= 2000) // Leave some buffer below the 2100 limit
            {
                return await GetPartsWithPrimaryVendorInternalAsync(
                    partNumbers: distinctPartNumbers, 
                    includeInactiveParts: includeInactiveParts, 
                    includeInactiveVendors: includeInactiveVendors);
            }

            // For large collections, batch the requests
            var results = new List<PartPrimaryVendorDTO>();
            const int batchSize = 2000;
            
            for (int i = 0; i < distinctPartNumbers.Count; i += batchSize)
            {
                var batch = distinctPartNumbers.Skip(i).Take(batchSize);
                var batchResults = await GetPartsWithPrimaryVendorInternalAsync(
                    partNumbers: batch,
                    includeInactiveParts: includeInactiveParts,
                    includeInactiveVendors: includeInactiveVendors);
                
                results.AddRange(batchResults);
            }

            return results;
        }
    }
}