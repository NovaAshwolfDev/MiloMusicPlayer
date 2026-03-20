using System;
using System.Collections.Generic;
using System.Text;

namespace MiloMusicPlayer.Scripting;

public sealed class LexerException : Exception
{
    public int Line   { get; }
    public int Column { get; }

    public LexerException(string message, int line, int column)
        : base($"[{line}:{column}] Lexer error: {message}")
    {
        Line   = line;
        Column = column;
    }
}

public sealed class Lexer
{
    private static readonly Dictionary<string, TokenType> Keywords = new()
    {
        ["var"]       = TokenType.Var,
        ["fun"]       = TokenType.Fun,
        ["func"]      = TokenType.Fun,
        ["fn"]        = TokenType.Fun,
        ["function"]  = TokenType.Fun,
        ["class"]     = TokenType.Class,
        ["interface"] = TokenType.Interface,
        ["enum"]      = TokenType.Enum,
        ["extends"]   = TokenType.Extends,
        ["implements"]= TokenType.Implements,
        ["return"]    = TokenType.Return,
        ["if"]        = TokenType.If,
        ["else"]      = TokenType.Else,
        ["while"]     = TokenType.While,
        ["for"]       = TokenType.For,
        ["in"]        = TokenType.In,
        ["new"]       = TokenType.New,
        ["this"]      = TokenType.This,
        ["import"]    = TokenType.Import,
        ["void"]      = TokenType.Void,
        ["true"]      = TokenType.BoolLiteral,
        ["false"]     = TokenType.BoolLiteral,
        ["null"]      = TokenType.NullLiteral,
        ["Int"]       = TokenType.TypeInt,
        ["Float"]     = TokenType.TypeFloat,
        ["String"]    = TokenType.TypeString,
        ["Bool"]      = TokenType.TypeBool,
    };

    private readonly string _source;
    private readonly List<Token> _tokens = new();

    private int _start   = 0;   
    private int _current = 0;   
    private int _line    = 1;
    private int _column  = 1;
    private int _startColumn = 1;

    public Lexer(string source)
    {
        _source = source;
    }

    public List<Token> Tokenize()
    {
        while (!IsAtEnd())
        {
            _start       = _current;
            _startColumn = _column;
            ScanToken();
        }

        _tokens.Add(new Token(TokenType.Eof, "", null, _line, _column));
        return _tokens;
    }

    private void ScanToken()
    {
        char c = Advance();

        switch (c)
        {
            case '(': AddToken(TokenType.LeftParen);    break;
            case ')': AddToken(TokenType.RightParen);   break;
            case '{': AddToken(TokenType.LeftBrace);    break;
            case '}': AddToken(TokenType.RightBrace);   break;
            case '[': AddToken(TokenType.LeftBracket);  break;
            case ']': AddToken(TokenType.RightBracket); break;
            case ',': AddToken(TokenType.Comma);        break;
            case ';': AddToken(TokenType.Semicolon);    break;
            case '.': AddToken(TokenType.Dot);          break;
            case '%': AddToken(TokenType.Percent);      break;
            case '*': AddToken(TokenType.Star);         break;

            case '+': AddToken(TokenType.Plus);  break;
            case '-':
                AddToken(Match('>') ? TokenType.Arrow : TokenType.Minus);
                break;
            case '=':
                AddToken(Match('=') ? TokenType.EqualsEquals : TokenType.Equals);
                break;
            case '!':
                AddToken(Match('=') ? TokenType.BangEquals : TokenType.Bang);
                break;
            case '<':
                AddToken(Match('=') ? TokenType.LessEquals : TokenType.Less);
                break;
            case '>':
                AddToken(Match('=') ? TokenType.GreaterEquals : TokenType.Greater);
                break;
            case '&':
                Expect('&', "Expected '&&'");
                AddToken(TokenType.And);
                break;
            case '|':
                Expect('|', "Expected '||'");
                AddToken(TokenType.Or);
                break;
            case ':': AddToken(TokenType.Colon); break;

            case '/':
                if (Match('/'))
                    SkipLineComment();
                else if (Match('*'))
                    SkipBlockComment();
                else
                    AddToken(TokenType.Slash);
                break;

            case ' ':
            case '\r':
            case '\t':
                break;

            case '\n':
                _line++;
                _column = 1;
                break;

            case '"':
                ScanString();
                break;

            default:
                if (IsDigit(c))
                    ScanNumber();
                else if (IsAlpha(c))
                    ScanIdentifierOrKeyword();
                else
                    throw new LexerException($"Unexpected character '{c}'", _line, _startColumn);
                break;
        }
    }


    private void ScanString()
    {
        var sb = new StringBuilder();

        while (!IsAtEnd() && Peek() != '"')
        {
            char c = Advance();

            if (c == '\n')
            {
                _line++;
                _column = 1;
            }

            if (c == '\\')
            {
                char esc = Advance();
                c = esc switch
                {
                    'n'  => '\n',
                    't'  => '\t',
                    'r'  => '\r',
                    '"'  => '"',
                    '\\' => '\\',
                    _    => throw new LexerException($"Unknown escape sequence '\\{esc}'", _line, _column)
                };
            }

            sb.Append(c);
        }

        if (IsAtEnd())
            throw new LexerException("Unterminated string literal", _line, _startColumn);

        Advance(); 
        AddToken(TokenType.StringLiteral, sb.ToString());
    }

    private void ScanNumber()
    {
        while (!IsAtEnd() && IsDigit(Peek()))
            Advance();

        bool isFloat = false;

        if (!IsAtEnd() && Peek() == '.' && IsDigit(PeekNext()))
        {
            isFloat = true;
            Advance(); 
            while (!IsAtEnd() && IsDigit(Peek()))
                Advance();
        }

        string text = _source[_start.._current];

        if (isFloat)
            AddToken(TokenType.FloatLiteral, float.Parse(text, System.Globalization.CultureInfo.InvariantCulture));
        else
            AddToken(TokenType.IntLiteral, int.Parse(text));
    }

    private void ScanIdentifierOrKeyword()
    {
        while (!IsAtEnd() && IsAlphaNumeric(Peek()))
            Advance();

        string text = _source[_start.._current];

        if (Keywords.TryGetValue(text, out TokenType kwType))
        {
            object? literal = kwType switch
            {
                TokenType.BoolLiteral => text == "true",
                TokenType.NullLiteral => null,
                _ => null
            };
            AddToken(kwType, literal);
        }
        else
        {
            AddToken(TokenType.Identifier);
        }
    }


    private void SkipLineComment()
    {
        while (!IsAtEnd() && Peek() != '\n')
            Advance();
    }

    private void SkipBlockComment()
    {
        int depth = 1; 

        while (!IsAtEnd() && depth > 0)
        {
            char c = Advance();

            if (c == '\n') { _line++; _column = 1; }
            else if (c == '/' && Peek() == '*') { Advance(); depth++; }
            else if (c == '*' && Peek() == '/') { Advance(); depth--; }
        }

        if (depth > 0)
            throw new LexerException("Unterminated block comment", _line, _startColumn);
    }


    private char Advance()
    {
        char c = _source[_current++];
        _column++;
        return c;
    }

    private bool Match(char expected)
    {
        if (IsAtEnd() || _source[_current] != expected)
            return false;
        Advance();
        return true;
    }

    private void Expect(char expected, string errorMessage)
    {
        if (!Match(expected))
            throw new LexerException(errorMessage, _line, _column);
    }

    private char Peek()      => IsAtEnd() ? '\0' : _source[_current];
    private char PeekNext()  => (_current + 1 >= _source.Length) ? '\0' : _source[_current + 1];
    private bool IsAtEnd()   => _current >= _source.Length;

    private static bool IsDigit(char c)        => c is >= '0' and <= '9';
    private static bool IsAlpha(char c)        => c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or '_';
    private static bool IsAlphaNumeric(char c) => IsAlpha(c) || IsDigit(c);

    private void AddToken(TokenType type, object? literal = null)
    {
        string lexeme = _source[_start.._current];
        _tokens.Add(new Token(type, lexeme, literal, _line, _startColumn));
    }
}