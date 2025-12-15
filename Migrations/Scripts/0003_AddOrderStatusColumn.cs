using FluentMigrator;

namespace Migrations.Scripts;
[Migration(3)]
public class AddOrderStatusColumn: Migration {
    public override void Up()
    {
        Execute.Sql(@"
            CREATE TYPE v1_order_status AS ENUM ('Created', 'Rejected', 'InAssembly', 'InDelivery', 'Completed' )
;
        ");

        Alter.Table("orders")
            .AddColumn("status")
            .AsCustom("v1_order_status")
            .NotNullable()
            .WithDefaultValue("Created");

        Execute.Sql(@"
            DROP TYPE IF EXISTS v1_order;
            CREATE TYPE v1_order AS (
                id bigint,
                customer_id bigint,
                delivery_address text,
                total_price_cents bigint,
                total_price_currency text,
                status v1_order_status,
                created_at timestamp with time zone,
                updated_at timestamp with time zone
            );
        ");
    }

    public override void Down()
    {
        Delete.Column("status").FromTable("orders");

        Execute.Sql(@"
            DROP TYPE IF EXISTS v1_order_status;
        ");

        Execute.Sql(@"
            DROP TYPE IF EXISTS v1_order;
            CREATE TYPE v1_order AS (
                id bigint,
                customer_id bigint,
                delivery_address text,
                total_price_cents bigint,
                total_price_currency text,
                created_at timestamp with time zone,
                updated_at timestamp with time zone
            );
        ");
    }
}
