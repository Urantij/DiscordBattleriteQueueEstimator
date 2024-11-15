using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace DiscordBattleriteQueueEstimator.Data;

// https://learn.microsoft.com/en-us/ef/core/modeling/bulk-configuration#example-default-length-for-all-string-properties
public class UlongConvection : IModelFinalizingConvention
{
    public void ProcessModelFinalizing(IConventionModelBuilder modelBuilder,
        IConventionContext<IConventionModelBuilder> context)
    {
        foreach (var property in modelBuilder.Metadata.GetEntityTypes()
                     .SelectMany(
                         entityType => entityType.GetDeclaredProperties()
                             .Where(
                                 property => property.ClrType == typeof(ulong))))
        {
            // TODO А чем отличается?
            property.Builder.Metadata.SetValueComparer(new UlongComparer());
            // property.Builder.HasValueComparer(new UlongComparer());
        }
    }
}