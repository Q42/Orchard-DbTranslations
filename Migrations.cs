using Orchard.Data.Migration;

namespace Q42.DbTranslations
{
  public class Migrations : DataMigrationImpl
  {

    public int Create()
    {
      SchemaBuilder.CreateTable(
          "LocalizableStringRecord",
          table =>
          table
              .Column<int>("Id", column => column.PrimaryKey().Identity())
              .Column<string>("Path")
              .Column<string>("Context")
              .Column<string>("StringKey", column => column.WithLength(4000))
              .Column<string>("OriginalLanguageString",
                              column => column.WithLength(4000))
          );
      SchemaBuilder.CreateTable(
          "TranslationRecord",
          table =>
          table
              .Column<int>("Id", column => column.PrimaryKey().Identity())
              .Column<string>("Culture")
              .Column<string>("Value", column => column.WithLength(4000))
              .Column<int>("LocalizableStringRecord_Id")
          )
		  //ForeignKey names must be unique, so enforce this using part of GUID
		  //Goes wrong when multitenancy is used with table prefixes in same database
          .CreateForeignKey(
			  string.Format("FK_Po_Translation_LocalizableString_{0}", System.Guid.NewGuid().ToString("N").Substring(0, 16).ToUpper()),
              "Q42.DbTranslations", "TranslationRecord",
              new[] { "LocalizableStringRecord_Id" },
              "Q42.DbTranslations", "LocalizableStringRecord",
              new[] { "Id" });
      SchemaBuilder.AlterTable(
          "TranslationRecord",
          table => table.CreateIndex(
              "Index_Po_Translation_LocalizableStringRecord_Id",
              "LocalizableStringRecord_Id"));
      return 1;
    }
    public int UpdateFrom1()
    {
        SchemaBuilder.ExecuteSql(@"DELETE FROM Q42_DbTranslations_LocalizableStringRecord WHERE [Path] LIKE '%_Backup%' OR Context LIKE '%_Backup%'");
        return 2;
    }

  }
}