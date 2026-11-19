using Microsoft.AspNetCore.Mvc;

namespace starfold_mobile.Controllers.Ezra;

[Route("ezra")]
public class NotesController : Controller
{
    // Placeholder landing page until the notes list arrives in step 3.
    [HttpGet("")]
    public IActionResult Index() => View();
}
