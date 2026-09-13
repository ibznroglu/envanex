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

        try
        {
            DataSourceLoadOptionsParser.Parse(options, key =>
                bindingContext.ValueProvider.GetValue(key).FirstValue);
        }
        catch (Exception)
        {
            bindingContext.ModelState.AddModelError(
                bindingContext.ModelName,
                "The datasource query string is malformed and could not be parsed.");
            bindingContext.Result = ModelBindingResult.Failed();
            return Task.CompletedTask;
        }

        bindingContext.Result = ModelBindingResult.Success(options);
        return Task.CompletedTask;
    }
}
