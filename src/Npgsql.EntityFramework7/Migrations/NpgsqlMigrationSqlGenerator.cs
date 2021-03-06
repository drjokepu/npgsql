using EntityFramework.Npgsql.Extensions;
using EntityFramework.Npgsql.Metadata;
using JetBrains.Annotations;
using Microsoft.Data.Entity.Metadata;
using Microsoft.Data.Entity.Relational;
using Microsoft.Data.Entity.Relational.Migrations.Operations;
using Microsoft.Data.Entity.Relational.Migrations.Sql;
using Microsoft.Data.Entity.Utilities;

namespace EntityFramework.Npgsql.Migrations
{
    public class NpgsqlMigrationSqlGenerator : MigrationSqlGenerator, INpgsqlMigrationSqlGenerator
    {
        private readonly INpgsqlSqlGenerator _sql;

        public NpgsqlMigrationSqlGenerator([NotNull] INpgsqlSqlGenerator sqlGenerator)
            : base(Check.NotNull(sqlGenerator, nameof(sqlGenerator)))
        {
            _sql = sqlGenerator;
        }

        public virtual void Generate(
            [NotNull] CreateDatabaseOperation operation,
            [CanBeNull] IModel model,
            [NotNull] SqlBatchBuilder builder)
        {
            Check.NotNull(operation, nameof(operation));
            Check.NotNull(builder, nameof(builder));

            builder
                .Append("CREATE DATABASE ")
                .Append(_sql.DelimitIdentifier(operation.Name))
                .EndBatch()
                .Append("IF SERVERPROPERTY('EngineEdition') <> 5 EXECUTE sp_executesql N'ALTER DATABASE ")
                .Append(_sql.DelimitIdentifier(operation.Name))
                .Append(" SET READ_COMMITTED_SNAPSHOT ON';");
        }

        public virtual void Generate(
            [NotNull] DropDatabaseOperation operation,
            [CanBeNull] IModel model,
            [NotNull] SqlBatchBuilder builder)
        {
            Check.NotNull(operation, nameof(operation));
            Check.NotNull(builder, nameof(builder));

            builder
                .Append("IF SERVERPROPERTY('EngineEdition') <> 5 EXECUTE sp_executesql N'ALTER DATABASE ")
                .Append(_sql.DelimitIdentifier(operation.Name))
                .Append(" SET SINGLE_USER WITH ROLLBACK IMMEDIATE'")
                .EndBatch()
                .Append("DROP DATABASE ")
                .Append(_sql.DelimitIdentifier(operation.Name))
                .Append(";");
        }

        protected override void Generate(RenameSequenceOperation operation, IModel model, SqlBatchBuilder builder)
        {
            Check.NotNull(operation, nameof(operation));
            Check.NotNull(builder, nameof(builder));

            GenerateRename(operation.Name, operation.Schema, operation.NewName, "OBJECT", builder);
        }

        protected override void Generate(MoveSequenceOperation operation, IModel model, SqlBatchBuilder builder)
        {
            Check.NotNull(operation, nameof(operation));
            Check.NotNull(builder, nameof(builder));

            GenerateMove(operation.Name, operation.Schema, operation.NewSchema, builder);
        }

        protected override void Generate(RenameTableOperation operation, IModel model, SqlBatchBuilder builder)
        {
            Check.NotNull(operation, nameof(operation));
            Check.NotNull(builder, nameof(builder));

            GenerateRename(operation.Name, operation.Schema, operation.NewName, "OBJECT", builder);
        }

        protected override void Generate(MoveTableOperation operation, IModel model, SqlBatchBuilder builder)
        {
            Check.NotNull(operation, nameof(operation));
            Check.NotNull(builder, nameof(builder));

            GenerateMove(operation.Name, operation.Schema, operation.NewSchema, builder);
        }

        protected override void GenerateColumn([NotNull] ColumnModel column, [NotNull] SqlBatchBuilder builder)
        {
            Check.NotNull(column, nameof(column));
            Check.NotNull(builder, nameof(builder));

            var computedSql = column[NpgsqlAnnotationNames.Prefix + NpgsqlAnnotationNames.ColumnComputedExpression];
            if (computedSql == null)
            {
                base.GenerateColumn(column, builder);

                return;
            }

            builder
                .Append(_sql.DelimitIdentifier(column.Name))
                .Append(" ")
                .Append("AS ")
                .Append(computedSql);
        }

        protected override void Generate(RenameColumnOperation operation, IModel model, SqlBatchBuilder builder)
        {
            Check.NotNull(operation, nameof(operation));
            Check.NotNull(builder, nameof(builder));

            GenerateRename(
                _sql.EscapeLiteral(operation.Table) + "." + _sql.EscapeLiteral(operation.Name),
                operation.Schema,
                operation.NewName,
                "COLUMN",
                builder);
        }

        protected override void GenerateIndexTraits(CreateIndexOperation operation, SqlBatchBuilder builder)
        {
            Check.NotNull(operation, nameof(operation));
            Check.NotNull(builder, nameof(builder));

            if (operation[NpgsqlAnnotationNames.Prefix + NpgsqlAnnotationNames.Clustered] == bool.TrueString)
            {
                builder.Append("CLUSTERED ");
            }
        }

        protected override void Generate(RenameIndexOperation operation, IModel model, SqlBatchBuilder builder)
        {
            Check.NotNull(operation, nameof(operation));
            Check.NotNull(builder, nameof(builder));

            GenerateRename(
                _sql.EscapeLiteral(operation.Table) + "." + _sql.EscapeLiteral(operation.Name),
                operation.Schema,
                operation.NewName,
                "INDEX",
                builder);
        }

        protected override void GenerateColumnTraits(ColumnModel column, SqlBatchBuilder builder)
        {
            Check.NotNull(column, nameof(column));
            Check.NotNull(builder, nameof(builder));

            if (column[NpgsqlAnnotationNames.Prefix + NpgsqlAnnotationNames.ValueGeneration] ==
                NpgsqlValueGenerationStrategy.Identity.ToString())
            {
                builder.Append(" IDENTITY");
            }
        }

        protected override void GeneratePrimaryKeyTraits(AddPrimaryKeyOperation operation, SqlBatchBuilder builder)
        {
            Check.NotNull(operation, nameof(operation));
            Check.NotNull(builder, nameof(builder));

            if (operation[NpgsqlAnnotationNames.Prefix + NpgsqlAnnotationNames.Clustered] != bool.TrueString)
            {
                builder.Append(" NONCLUSTERED");
            }
        }

        private void GenerateRename(
            [NotNull] string name,
            [CanBeNull] string schema,
            [NotNull] string newName,
            [NotNull] string objectType,
            [NotNull] SqlBatchBuilder builder)
        {
            builder.Append("EXECUTE sp_rename @objname = N");

            if (!string.IsNullOrWhiteSpace(schema))
            {
                builder
                    .Append(_sql.GenerateLiteral(schema))
                    .Append(".");
            }

            builder
                .Append(_sql.GenerateLiteral(name))
                .Append(", @newname = N")
                .Append(_sql.GenerateLiteral(newName))
                .Append(", @objtype = N")
                .Append(_sql.GenerateLiteral(objectType))
                .Append(";");
        }

        private void GenerateMove(
            [NotNull] string name,
            [CanBeNull] string schema,
            [NotNull] string newSchema,
            [NotNull] SqlBatchBuilder builder) =>
                builder
                    .Append("ALTER SCHEMA ")
                    .Append(_sql.DelimitIdentifier(newSchema))
                    .Append(" TRANSFER ")
                    .Append(_sql.DelimitIdentifier(name, schema))
                    .Append(";");
    }
}
