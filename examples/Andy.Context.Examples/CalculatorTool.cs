using Andy.Context.Model;
using Andy.Context.Tooling;

namespace Andy.Context.Examples;

/// <summary>
/// Simple calculator tool for demonstration. Evaluates +,-,*,/ with integer/decimal numbers.
/// </summary>
public sealed class CalculatorTool : ITool
{
    public ToolDeclaration Definition { get; } = new()
    {
        Name = "calculator",
        Description = "Evaluates a basic arithmetic expression (e.g., '2+2*3').",
        Parameters = new Dictionary<string, object>
        {
            ["type"] = "object",
            ["properties"] = new Dictionary<string, object>
            {
                ["expression"] = new Dictionary<string, object>
                {
                    ["type"] = "string"
                }
            },
            ["required"] = new[] { "expression" }
        }
    };

    public Task<ToolResult> ExecuteAsync(ToolCall call, CancellationToken ct = default)
    {
        var args = call.ArgumentsAsJsonElement();
        var expr = args.TryGetProperty("expression", out var e) ? e.GetString() ?? string.Empty : string.Empty;
        try
        {
            var val = Evaluate(expr);
            return Task.FromResult(ToolResult.FromObject(call.Id, Definition.Name, new { ok = true, expression = expr, value = val }));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolResult.FromObject(call.Id, Definition.Name, new { ok = false, error = ex.Message }, isError: true));
        }
    }

    // Extremely small expression evaluator (no external deps). Not robust; just for demo.
    private static double Evaluate(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression)) throw new ArgumentException("Empty expression");
        var tokens = Tokenize(expression);
        var rpn = ToRpn(tokens);
        return EvalRpn(rpn);
    }

    private enum TokType { Num, Op, LPar, RPar }
    private readonly record struct Tok(TokType T, string S);

    private static List<Tok> Tokenize(string s)
    {
        var list = new List<Tok>();
        int i = 0;
        while (i < s.Length)
        {
            if (char.IsWhiteSpace(s[i])) { i++; continue; }
            if (char.IsDigit(s[i]) || s[i] == '.')
            {
                int j = i + 1;
                while (j < s.Length && (char.IsDigit(s[j]) || s[j] == '.')) j++;
                list.Add(new Tok(TokType.Num, s.Substring(i, j - i)));
                i = j; continue;
            }
            if ("+-*/".IndexOf(s[i]) >= 0)
            {
                list.Add(new Tok(TokType.Op, s[i].ToString())); i++; continue;
            }
            if (s[i] == '(') { list.Add(new Tok(TokType.LPar, "(")); i++; continue; }
            if (s[i] == ')') { list.Add(new Tok(TokType.RPar, ")")); i++; continue; }
            throw new FormatException($"Unexpected char '{s[i]}' at {i}");
        }
        return list;
    }

    private static int Prec(string op) => op is "+" or "-" ? 1 : 2;

    private static List<Tok> ToRpn(List<Tok> toks)
    {
        var output = new List<Tok>();
        var ops = new Stack<Tok>();
        foreach (var t in toks)
        {
            switch (t.T)
            {
                case TokType.Num: output.Add(t); break;
                case TokType.Op:
                    while (ops.Count > 0 && ops.Peek().T == TokType.Op && Prec(ops.Peek().S) >= Prec(t.S))
                        output.Add(ops.Pop());
                    ops.Push(t);
                    break;
                case TokType.LPar: ops.Push(t); break;
                case TokType.RPar:
                    while (ops.Count > 0 && ops.Peek().T != TokType.LPar) output.Add(ops.Pop());
                    if (ops.Count == 0 || ops.Peek().T != TokType.LPar) throw new FormatException("Mismatched parentheses");
                    ops.Pop();
                    break;
            }
        }
        while (ops.Count > 0)
        {
            var op = ops.Pop();
            if (op.T is TokType.LPar or TokType.RPar) throw new FormatException("Mismatched parentheses");
            output.Add(op);
        }
        return output;
    }

    private static double EvalRpn(List<Tok> rpn)
    {
        var st = new Stack<double>();
        foreach (var t in rpn)
        {
            if (t.T == TokType.Num)
            {
                st.Push(double.Parse(t.S, System.Globalization.CultureInfo.InvariantCulture));
            }
            else if (t.T == TokType.Op)
            {
                if (st.Count < 2) throw new FormatException("Operator arity error");
                var b = st.Pop();
                var a = st.Pop();
                st.Push(t.S switch
                {
                    "+" => a + b,
                    "-" => a - b,
                    "*" => a * b,
                    "/" => a / b,
                    _ => throw new NotSupportedException()
                });
            }
        }
        if (st.Count != 1) throw new FormatException("Evaluation error");
        return st.Pop();
    }
}