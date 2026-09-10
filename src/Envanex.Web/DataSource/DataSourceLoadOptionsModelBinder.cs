using DevExtreme.AspNet.Data;
using DevExtreme.AspNet.Data.Helpers;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Envanex.Web.DataSource;

public sealed class DataSourceLoadOptionsModelBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        ArgumentNullException.ThrowIfNull(bindingContext);

        var options = new DataSourceLoadOptionsBase();
        DataSourceLoadOptionsParser.Parse(options, key =>
            bindingContext.ValueProvider.GetValue(key).FirstValue);

        bindingContext.Result = ModelBindingResult.Success(options);
        return Task.CompletedTask;
    }
}
