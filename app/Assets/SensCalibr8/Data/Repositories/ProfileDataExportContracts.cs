using System;
using System.Collections.Generic;
using System.Linq;

namespace SensCalibr8.Data.Repositories
{
    public static class ProfileDataExportContract { public const string Version = "sc8-profile-data-export-v1"; public const string Disclaimer = "Data Export only. Not an Import/Restore backup or recovery guarantee."; }
    public sealed class ProfileDataExportTable
    {
        public ProfileDataExportTable(string name,IReadOnlyList<string> columns,IReadOnlyList<IReadOnlyDictionary<string,object>> rows)
        {Name=Required(name);Columns=(columns??throw new ArgumentNullException(nameof(columns))).ToArray();Rows=(rows??throw new ArgumentNullException(nameof(rows))).ToArray();if(Columns.Count==0||Columns.Any(string.IsNullOrWhiteSpace)||Columns.Distinct(StringComparer.Ordinal).Count()!=Columns.Count)throw new ArgumentException("Export columns must be unique and non-empty.");foreach(var row in Rows)if(row==null||Columns.Any(column=>!row.ContainsKey(column)))throw new ArgumentException("Export row does not match its columns.");}
        public string Name{get;}public IReadOnlyList<string> Columns{get;}public IReadOnlyList<IReadOnlyDictionary<string,object>> Rows{get;}
        private static string Required(string value)=>!string.IsNullOrWhiteSpace(value)?value:throw new ArgumentException("Export table name is required.");
    }
    public sealed class ProfileDataExportDataset
    {
        public ProfileDataExportDataset(AnalysisProfileIdentity profile,IReadOnlyList<ProfileDataExportTable> tables)
        {ExportVersion=ProfileDataExportContract.Version;Profile=profile??throw new ArgumentNullException(nameof(profile));Tables=(tables??throw new ArgumentNullException(nameof(tables))).ToArray();if(Tables.Select(value=>value.Name).Distinct(StringComparer.Ordinal).Count()!=Tables.Count)throw new ArgumentException("Export table names must be unique.");}
        public string ExportVersion{get;}public AnalysisProfileIdentity Profile{get;}public IReadOnlyList<ProfileDataExportTable> Tables{get;}
    }
}
