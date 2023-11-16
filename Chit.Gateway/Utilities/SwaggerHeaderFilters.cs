using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Chit.Gateway;

public class SwaggerHeaderFilters: IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (operation.Parameters == null)
            operation.Parameters = new List<OpenApiParameter>();
 
        operation.Parameters.Add(new OpenApiParameter
        {
            Name = "Referrer",
            In = ParameterLocation.Header,
            Description = "This header is used to determine if the request is coming from swagger or not",
            Required = false, // set to false if this is optional
            Schema = new OpenApiSchema
            {
                Type = "string",
                Default = new OpenApiString("swagger")
            },

        });
    }
}
