using InvoiceNudge.Application.Abstractions;
using Scriban;
using Scriban.Runtime;

namespace InvoiceNudge.Infrastructure.Templating;

/// <summary>
/// Renders reminder templates with Scriban. Member names are exposed in snake_case
/// (e.g. {{ client_name }} for ClientName), which is Scriban's default convention.
/// </summary>
public sealed class ScribanTemplateRenderer : ITemplateRenderer
{
    public string Render(string template, object model)
    {
        var parsed = Template.Parse(template);
        if (parsed.HasErrors)
            throw new InvalidOperationException("Template parse error: " +
                string.Join("; ", parsed.Messages.Select(m => m.ToString())));

        var scriptObject = new ScriptObject();
        scriptObject.Import(model);

        var context = new TemplateContext
        {
            MemberRenamer = member => StandardMemberRenamer.Rename(member),
            StrictVariables = false
        };
        context.PushGlobal(scriptObject);

        return parsed.Render(context);
    }
}
