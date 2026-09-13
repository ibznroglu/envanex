using DevExtreme.AspNet.Data;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Envanex.Web.DataSource;

public sealed class DataSourceLoadOptionsModelBinderProvider : IModelBinderProvider
{
    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Metadata.ModelType == typeof(DataSourceLoadOptionsBase))
        {
            return new DataSourceLoadOptionsModelBinder();
        }

        return null;
    }
}
