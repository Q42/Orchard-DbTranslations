﻿using Orchard.Data.Migration;

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
          .CreateForeignKey(
              "FK_Po_Translation_LocalizableString",
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
  }
}