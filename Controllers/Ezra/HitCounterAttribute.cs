using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using starfold_mobile.Data;

namespace starfold_mobile.Controllers.Ezra;

// Counts Ezra page views for the layout's odometer. Only full pages count
// (GET requests that render a view), and signed-in visits are shown the
// count without adding to it.
public class HitCounterAttribute : Attribute, IAsyncResultFilter
{
    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        var http = context.HttpContext;
        if (context.Result is ViewResult view && (view.StatusCode ?? 200) == 200 && HttpMethods.IsGet(http.Request.Method))
        {
            var db = http.RequestServices.GetRequiredService<EzraDbContext>();
            view.ViewData["Hits"] = http.User.Identity?.IsAuthenticated == true
                ? await db.HitCounters.Where(h => h.Id == 1).Select(h => h.Count).SingleAsync()
                : (await db.Database.SqlQueryRaw<long>(
                    "UPDATE ezra_hit_counter SET count = count + 1 WHERE id = 1 RETURNING count AS Value").ToListAsync()).Single();
        }
        await next();
    }
}
