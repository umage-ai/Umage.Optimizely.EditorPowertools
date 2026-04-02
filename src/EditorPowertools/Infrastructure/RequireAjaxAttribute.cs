using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace EditorPowertools.Infrastructure;

/// <summary>
/// Requires X-Requested-With header on POST/PUT/DELETE requests as CSRF mitigation.
/// Browsers won't send this header cross-origin without CORS preflight approval.
/// </summary>
public class RequireAjaxAttribute : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var method = context.HttpContext.Request.Method;
        if (method is "POST" or "PUT" or "DELETE")
        {
            var xhr = context.HttpContext.Request.Headers["X-Requested-With"].FirstOrDefault();
            if (!string.Equals(xhr, "XMLHttpRequest", StringComparison.OrdinalIgnoreCase))
            {
                context.Result = new BadRequestObjectResult(new { error = "Missing X-Requested-With header" });
                return;
            }
        }
        base.OnActionExecuting(context);
    }
}
