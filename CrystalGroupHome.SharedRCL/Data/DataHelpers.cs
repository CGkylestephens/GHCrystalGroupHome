using Microsoft.VisualBasic;
using System.Reflection;

namespace CrystalGroupHome.SharedRCL.Data
{
    public class DataHelpers
    {
        public static string DTOPropertiesToSQLColumnsString<T>()
        {
            var propertyInfos = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);
            var columns = propertyInfos
                .Where(prop => prop.GetCustomAttribute<ExcludeFromTableColumnAttribute>() == null) // Skip excluded props
                .Select(prop =>
                {
                    var attr = prop.GetCustomAttribute<TableColumnAttribute>();
                    if (attr != null)
                    {
                        // Use the custom column name if provided; otherwise, default to the property name.
                        var columnName = string.IsNullOrEmpty(attr.ColumnName) ? prop.Name : attr.ColumnName;
                        return $"{attr.TableAlias}.{columnName} as {prop.Name}";
                    }
                    else
                    {
                        // If no attribute is provided, assume no alias is required.
                        return prop.Name;
                    }
                });

            return string.Join(", ", columns!);
        }

        public static string DTOPropertiesToSQLColumnsString<T>(string tablePrefix)
        {
            var propertyList = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                            .Select(p => $"{tablePrefix}.{p.Name}") // Prepend tablePrefix to each property
                            .ToList();

            var queryColumns = string.Empty;

            if (propertyList != null && propertyList.Count > 0)
            {
                queryColumns = string.Join(", ", propertyList);
            }

            return queryColumns;
        }

        // Checks if a DateTime is within a reasonable SQL range
        public static bool IsBusinessReasonableDateTime(DateTime? inputDate)
        {
            if (inputDate == null)
            {
                return false;
            }
            DateTime minDate = new(1900, 1, 1);
            DateTime maxDate = new(9999, 12, 31);
            return inputDate >= minDate && inputDate <= maxDate;
        }

        // Returns a SQL-safe DateTime value within a reasonable range
        public static DateTime? ReturnBusinessReasonableDateTime(DateTime? inputDate)
        {
            if (inputDate == null)
            {
                return null;
            }
            DateTime minDate = new(1900, 1, 1);
            DateTime maxDate = new(9999, 12, 31);
            if (inputDate < minDate)
            {
                return minDate;
            }
            else if (inputDate > maxDate)
            {
                return maxDate;
            }
            else
            {
                return inputDate;
            }
        }
    }
}
