using System;
using System.Text;

namespace Mapo.Generator.Emit;

public class CodeWriter
{
    private readonly StringBuilder _sb;
    private int _indent;
    private bool _returned;
    private static readonly string[] IndentationCache = CreateIndentationCache(16);

    public CodeWriter()
    {
        _sb = StringBuilderPool.Rent();
    }

    private static string[] CreateIndentationCache(int max)
    {
        var cache = new string[max];
        for (int i = 0; i < max; i++)
        {
            cache[i] = new string(' ', i * 4);
        }
        return cache;
    }

    public void Indent() => _indent++;

    public void Dedent() => _indent = Math.Max(0, _indent - 1);

    private string GetIndentation()
    {
        if (_indent < IndentationCache.Length)
        {
            return IndentationCache[_indent];
        }
        return new string(' ', _indent * 4);
    }

    public void AppendLine(string text = "")
    {
        if (string.IsNullOrEmpty(text))
        {
            _sb.AppendLine();
        }
        else
        {
            _sb.Append(GetIndentation());
            _sb.AppendLine(text);
        }
    }

    public void Append(string text)
    {
        _sb.Append(text);
    }

    public IDisposable Block()
    {
        AppendLine("{");
        Indent();
        return new BlockDisposable(this);
    }

    public override string ToString()
    {
        if (_returned)
            throw new InvalidOperationException("CodeWriter.ToString() has already been called.");
        _returned = true;
        return StringBuilderPool.ToStringAndReturn(_sb);
    }

    private class BlockDisposable : IDisposable
    {
        private readonly CodeWriter _writer;

        public BlockDisposable(CodeWriter writer)
        {
            _writer = writer;
        }

        public void Dispose()
        {
            _writer.Dedent();
            _writer.AppendLine("}");
        }
    }
}
