namespace CrystalGroupHome.SharedRCL.Data
{
    /// <summary>
    /// Specifies the table alias and optional column name for a property when generating SQL queries.
    /// Used by DataHelpers to map DTO properties to database columns with table prefixes.
    /// </summary>
    /// <param name="tableAlias">The table alias or name to prefix the column with</param>
    /// <param name="columnName">Optional column name if it differs from the property name</param>
    [AttributeUsage(AttributeTargets.Property)]
    public class TableColumnAttribute(string tableAlias, string? columnName = null) : Attribute
    {
        /// <summary>
        /// The table alias or name for the column.
        /// </summary>
        public string TableAlias { get; } = tableAlias;

        /// <summary>
        /// Optionally override the column name if it differs from the property name.
        /// </summary>
        public string? ColumnName { get; } = columnName;
    }

    /// <summary>
    /// Marks a property to be excluded from SQL column generation when using DataHelpers.
    /// Properties with this attribute will be skipped when building SQL SELECT statements.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class ExcludeFromTableColumnAttribute : Attribute
    {
    }
}
