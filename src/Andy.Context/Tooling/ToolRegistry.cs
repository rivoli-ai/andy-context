namespace Andy.Context.Tooling;

/// <summary>
/// Registry to manage tools available to an assistant/agent.
/// </summary>
public sealed class ToolRegistry
{
    private readonly Dictionary<string, ITool> _tools = new(StringComparer.OrdinalIgnoreCase);

    public void Register(ITool tool)
    {
        _tools[tool.Definition.Name] = tool;
    }

    public void Unregister(string toolName)
    {
        _tools.Remove(toolName);
    }

    public ToolDeclaration[] GetDeclaredTools() => _tools.Values.Select(t => t.Definition).ToArray();

    public bool TryGet(string name, out ITool tool) => _tools.TryGetValue(name, out tool!);

    public bool IsRegistered(string name) => _tools.ContainsKey(name);

    public IReadOnlyList<string> GetRegisteredToolNames() => _tools.Keys.ToList();
}