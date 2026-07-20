using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Qistas.Api.Swagger;

/// <summary>
/// Document-inclusion helper used by Program.cs's SwaggerGenOptions.DocInclusionPredicate
/// to split endpoints between the two Swagger documents ("balance" and "admin") by their
/// .WithGroupName(...) metadata (PLAN.md 1.5: "two SwaggerDoc entries ... rendered as two
/// selectable docs"). Kept as a small static helper rather than an IDocumentFilter so the
/// split happens before generation instead of pruning paths after the fact.
/// </summary>
public static class SwaggerGroupDocumentFilter
{
    public static bool BelongsToDocument(string documentName, ApiDescription apiDescription) =>
        string.Equals(apiDescription.GroupName, documentName, StringComparison.OrdinalIgnoreCase);
}
