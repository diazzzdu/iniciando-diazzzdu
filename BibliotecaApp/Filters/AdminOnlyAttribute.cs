using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace BibliotecaApp.Filters
{
    public class AdminOnlyAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var email = context.HttpContext.Session.GetString("AdminEmail");
            if (string.IsNullOrEmpty(email))
            {
                context.Result = new RedirectToActionResult("Login", "Conta", null);
            }
            base.OnActionExecuting(context);
        }
    }
}
