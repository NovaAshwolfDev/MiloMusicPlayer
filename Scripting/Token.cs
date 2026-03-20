namespace MiloMusicPlayer.Scripting;

public enum TokenType
{
    IntLiteral,
    FloatLiteral,
    StringLiteral,
    BoolLiteral,    
    NullLiteral,    

    Identifier,

    Var,
    Fun,
    Class,
    Interface,
    Enum,
    Extends,
    Implements,
    Return,
    If,
    Else,
    While,
    For,
    In,
    New,
    This,
    Import,
    Void,

    TypeInt,
    TypeFloat,
    TypeString,
    TypeBool,

    Plus,           
    Minus,          
    Star,           
    Slash,          
    Percent,        
    Equals,         
    EqualsEquals,   
    BangEquals,     
    Less,           
    LessEquals,     
    Greater,        
    GreaterEquals,  
    And,            
    Or,             
    Bang,           
    Dot,            
    Arrow,          

    LeftParen,      
    RightParen,     
    LeftBrace,      
    RightBrace,     
    LeftBracket,    
    RightBracket,   
    Comma,          
    Colon,          
    Semicolon,      
    Eof,
    Unknown
}

public sealed class Token
{
    public TokenType Type     { get; }
    public string    Lexeme   { get; }  
    public object?   Literal  { get; }  
    public int       Line     { get; }
    public int       Column   { get; }

    public Token(TokenType type, string lexeme, object? literal, int line, int column)
    {
        Type    = type;
        Lexeme  = lexeme;
        Literal = literal;
        Line    = line;
        Column  = column;
    }

    public override string ToString() =>
        $"[{Line}:{Column}] {Type} '{Lexeme}'" +
        (Literal is not null ? $" = {Literal}" : "");
}