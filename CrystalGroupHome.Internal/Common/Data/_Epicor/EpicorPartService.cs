using CrystalGroupHome.SharedRCL.Data;
using CrystalGroupHome.SharedRCL.Data.Parts;
using EpicorRestAPI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Reflection;

namespace CrystalGroupHome.Internal.Common.Data._Epicor
{
    public interface IEpicorPartService
    {
        Task<bool> SetCMDataAsync(string partNum, bool cmManaged, DateTime? origDate);
        Task<bool> UpdatePartAsync<T>(T partDto) where T : PartDTO_Base;
    }

    public class EpicorPartService : IEpicorPartService
    {
        private readonly EpicorRest _client;

        public EpicorPartService(EpicorRestInitializer initializer)
        {
            _client = initializer.Client;
        }

        public async Task<bool> SetCMDataAsync(string partNum, bool cmManaged, DateTime? origDate)
        {
            var getResponse = await Task.Run(() =>
                _client.BoGet("Erp.BO.PartSvc", $"Parts('{_client.Company}','{partNum}')")
            );

            if (getResponse.IsErrorResponse)
            {
                Console.WriteLine("Failed to retrieve part: " + getResponse.ResponseError);
                return false;
            }

            var partData = getResponse.ResponseData;
            partData["CM_CMManaged_c"] = cmManaged;
            partData["CM_CMOriginationDate_c"] = origDate != null ? ((DateTime)origDate).ToUniversalTime().ToString("o") : DateTime.MinValue.ToUniversalTime().ToString("o");

            var data = JsonConvert.SerializeObject(partData);
            var updateResponse = await Task.Run(() =>
                _client.BoPatch("Erp.BO.PartSvc", $"Parts('{_client.Company}','{partNum}')", data)
            );

            if (updateResponse.IsErrorResponse)
            {
                Console.WriteLine("Failed to update part: " + updateResponse.ResponseError);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Updates a part in Epicor using all properties from the provided DTO.
        /// </summary>
        /// <typeparam name="T">The PartDTO type to update.</typeparam>
        /// <param name="partDto">The PartDTO containing the updated values.</param>
        /// <returns>True if the update was successful, false otherwise.</returns>
        public async Task<bool> UpdatePartAsync<T>(T partDto) where T : PartDTO_Base
        {
            if (partDto == null || string.IsNullOrWhiteSpace(partDto.PartNum))
            {
                Console.WriteLine("Invalid part DTO or part number.");
                return false;
            }

            // Get existing part data from Epicor
            var getResponse = await Task.Run(() =>
                _client.BoGet("Erp.BO.PartSvc", $"Parts('{_client.Company}','{partDto.PartNum}')")
            );

            if (getResponse.IsErrorResponse)
            {
                Console.WriteLine("Failed to retrieve part: " + getResponse.ResponseError);
                return false;
            }

            var partData = getResponse.ResponseData;

            // Map all DTO properties to Epicor field names using reflection and TableColumn attributes
            MapDtoPropertiesToEpicorData(partDto, partData);

            // Serialize and update
            var data = JsonConvert.SerializeObject(partData);
            var updateResponse = await Task.Run(() =>
                _client.BoPatch("Erp.BO.PartSvc", $"Parts('{_client.Company}','{partDto.PartNum}')", data)
            );

            if (updateResponse.IsErrorResponse)
            {
                Console.WriteLine("Failed to update part: " + updateResponse.ResponseError);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Maps DTO properties to Epicor part data using TableColumn attributes.
        /// Only includes properties where the corresponding field exists in the Epicor part data.
        /// </summary>
        private static void MapDtoPropertiesToEpicorData<T>(T partDto, dynamic partData) where T : PartDTO_Base
        {
            // It's entirely possible that this does not work for ALL Epicor-accepted types...
            // I've only tested it with a few, and the rest of this is AI generated.

            var properties = typeof(T).GetProperties();

            foreach (var property in properties)
            {
                // Get the TableColumn attribute to determine the Epicor field name
                var tableColumnAttr = property.GetCustomAttribute<TableColumnAttribute>();

                if (tableColumnAttr != null)
                {
                    // Use the column name from the attribute, or property name as fallback
                    var epicorFieldName = !string.IsNullOrEmpty(tableColumnAttr.ColumnName)
                        ? tableColumnAttr.ColumnName
                        : property.Name;

                    // Check if this field actually exists in the Epicor part data
                    // Handle both JObject and IDictionary<string, object> cases
                    bool fieldExists = false;
                    if (partData is JObject jObject)
                    {
                        fieldExists = jObject.ContainsKey(epicorFieldName);
                    }
                    else if (partData is IDictionary<string, object> dictionary)
                    {
                        fieldExists = dictionary.ContainsKey(epicorFieldName);
                    }

                    if (!fieldExists)
                    {
                        continue; // Skip fields that don't exist in the Epicor part data
                    }

                    var value = property.GetValue(partDto);

                    // Check if the property type is nullable DateTime first
                    var underlyingType = Nullable.GetUnderlyingType(property.PropertyType);
                    bool isNullableDateTime = underlyingType == typeof(DateTime);

                    // Handle different data types appropriately
                    if (value != null)
                    {
                        // Handle DateTime values (both DateTime and DateTime?)
                        if (value is DateTime dateTimeValue)
                        {
                            partData[epicorFieldName] = dateTimeValue.ToUniversalTime().ToString("o");
                        }
                        // Handle DateOnly (.NET 6+)
                        else if (value is DateOnly dateOnlyValue)
                        {
                            partData[epicorFieldName] = dateOnlyValue.ToDateTime(TimeOnly.MinValue).ToUniversalTime().ToString("o");
                        }
                        // Handle TimeOnly (.NET 6+)
                        else if (value is TimeOnly timeOnlyValue)
                        {
                            partData[epicorFieldName] = timeOnlyValue.ToString("HH:mm:ss");
                        }
                        // Handle TimeSpan
                        else if (value is TimeSpan timeSpanValue)
                        {
                            partData[epicorFieldName] = timeSpanValue.ToString("c");
                        }
                        // Handle Guid
                        else if (value is Guid guidValue)
                        {
                            partData[epicorFieldName] = guidValue.ToString();
                        }
                        // Handle numeric types
                        else if (value is int)
                        {
                            partData[epicorFieldName] = (int)value;
                        }
                        else if (value is short)
                        {
                            partData[epicorFieldName] = (short)value;
                        }
                        else if (value is long)
                        {
                            partData[epicorFieldName] = (long)value;
                        }
                        else if (value is byte)
                        {
                            partData[epicorFieldName] = (byte)value;
                        }
                        // Handle unsigned numeric types
                        else if (value is uint || value is ushort || value is ulong)
                        {
                            partData[epicorFieldName] = value;
                        }
                        // Handle decimal types
                        else if (value is decimal decimalValue)
                        {
                            partData[epicorFieldName] = decimalValue;
                        }
                        else if (value is double doubleValue)
                        {
                            partData[epicorFieldName] = doubleValue;
                        }
                        else if (value is float floatValue)
                        {
                            partData[epicorFieldName] = floatValue;
                        }
                        // Handle boolean
                        else if (value is bool boolValue)
                        {
                            partData[epicorFieldName] = boolValue;
                        }
                        // Handle string
                        else if (value is string stringValue)
                        {
                            partData[epicorFieldName] = stringValue;
                        }
                        // Handle char
                        else if (value is char charValue)
                        {
                            partData[epicorFieldName] = charValue.ToString();
                        }
                        // Handle byte arrays
                        else if (value is byte[] byteArrayValue)
                        {
                            partData[epicorFieldName] = Convert.ToBase64String(byteArrayValue);
                        }
                        // Handle enums
                        else if (value.GetType().IsEnum)
                        {
                            partData[epicorFieldName] = value.ToString();
                        }
                        // Handle nullable types by getting the underlying type
                        else if (underlyingType != null)
                        {
                            if (underlyingType == typeof(DateTime))
                            {
                                partData[epicorFieldName] = ((DateTime)value).ToUniversalTime().ToString("o");
                            }
                            else if (underlyingType == typeof(DateOnly))
                            {
                                partData[epicorFieldName] = ((DateOnly)value).ToDateTime(TimeOnly.MinValue).ToUniversalTime().ToString("o");
                            }
                            else if (underlyingType == typeof(TimeOnly))
                            {
                                partData[epicorFieldName] = ((TimeOnly)value).ToString("HH:mm:ss");
                            }
                            else if (underlyingType == typeof(TimeSpan))
                            {
                                partData[epicorFieldName] = ((TimeSpan)value).ToString("c");
                            }
                            else if (underlyingType == typeof(Guid))
                            {
                                partData[epicorFieldName] = ((Guid)value).ToString();
                            }
                            else if (underlyingType.IsEnum)
                            {
                                partData[epicorFieldName] = value.ToString();
                            }
                            else
                            {
                                // For other nullable types, pass the value directly
                                partData[epicorFieldName] = value;
                            }
                        }
                        // Handle all other types directly
                        else
                        {
                            partData[epicorFieldName] = value;
                        }
                    }
                    else
                    {
                        // Handle null values - special case for nullable DateTime
                        if (isNullableDateTime)
                        {
                            // For nullable DateTime types that are null, set to 01-01-0001 for Epicor API
                            partData[epicorFieldName] = new DateTime(1, 1, 1).ToUniversalTime().ToString("o");
                        }
                        else if (underlyingType == typeof(DateOnly))
                        {
                            // For nullable DateOnly types, set to a default date if null
                            partData[epicorFieldName] = new DateTime(1, 1, 1).ToUniversalTime().ToString("o");
                        }
                        else if (underlyingType == typeof(TimeOnly))
                        {
                            // For nullable TimeOnly types, set to a default time if null
                            partData[epicorFieldName] = TimeOnly.MinValue.ToString("HH:mm:ss");
                        }
                        else
                        {
                            // Handle null values - set them to null for non-DateTime types
                            partData[epicorFieldName] = null;
                        }
                    }
                }
            }
        }
    }
}