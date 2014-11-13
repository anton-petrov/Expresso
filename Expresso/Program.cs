using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Runtime.InteropServices;

namespace Expresso
{
    public class QueryPerfCounter
    {
        [DllImport("KERNEL32")]
        private static extern bool QueryPerformanceCounter(
          out long lpPerformanceCount);

        [DllImport("Kernel32.dll")]
        private static extern bool QueryPerformanceFrequency(out long lpFrequency);

        private long start;
        private long stop;
        private long frequency;
        Decimal multiplier = new Decimal(1.0e9);

        public QueryPerfCounter()
        {
            if (QueryPerformanceFrequency(out frequency) == false)
            {
                // Frequency not supported
                //throw new Win32Exception();
            }
        }

        public void Start()
        {
            QueryPerformanceCounter(out start);
        }

        public void Stop()
        {
            QueryPerformanceCounter(out stop);
        }

        public double Duration(int iterations)
        {
            return ((((double)(stop - start) * (double)multiplier) / (double)frequency) / iterations);
        }
    }

    class RandGen
    {
        public const int rndbasemax = 2147483563;
        public const double rndbasemaxr = 1.0 / rndbasemax;
        public const int rndbasem1 = 2147483563;
        public const int rndbasem2 = 2147483399;
        public static int rndbases1 = 0;
        public static int rndbases2 = 0;

        public static System.Random RndObject = new System.Random();


        /*************************************************************************
        Генератор равномерно распределенных случайных вещественных чисел
        в диапазоне (0, 1)

          -- ALGLIB --
             Copyright 11.08.2007 by Bochkanov Sergey
        *************************************************************************/
        public static double rnduniformr()
        {
            double result = 0;

            result = rndbasemaxr * rndintegerbase();
            return result;
        }


        /*************************************************************************
        Генератор равномерно распределенных случайных целых чисел
        в диапазоне [0, N)

          -- ALGLIB --
             Copyright 11.08.2007 by Bochkanov Sergey
        *************************************************************************/
        public static int rnduniformi(int n)
        {
            int result = 0;

            System.Diagnostics.Debug.Assert(n > 0, "RndUniformI: N<=0!");
            System.Diagnostics.Debug.Assert(n < rndbasemax, "RndUniformI: N>RNDBaseMax!");
            result = rndintegerbase() % n;
            return result;
        }

        public static double rndnormal()
        {
            double result = 0;
            double v1 = 0;
            double v2 = 0;

            rndnormal2(ref v1, ref v2);
            result = v1;
            return result;
        }

        public static void rndnormal2(ref double x1,
            ref double x2)
        {
            double u = 0;
            double v = 0;
            double s = 0;

            while (true)
            {
                u = 2 * rnduniformr() - 1;
                v = 2 * rnduniformr() - 1;
                s = (u * u) + (v * v);
                if (s > 0 & s < 1)
                {
                    s = Math.Sqrt(-(2 * Math.Log(s) / s));
                    x1 = u * s;
                    x2 = v * s;
                    return;
                }
            }
        }

        public static int rndintegerbase()
        {
            int result = 0;
            int k = 0;


            //
            // Initialize S1 and S2 if needed
            //
            if (rndbases1 < 1 | rndbases1 >= rndbasem1)
            {
                rndbases1 = 1 + RndObject.Next(32000);
            }
            if (rndbases2 < 1 | rndbases2 >= rndbasem2)
            {
                rndbases2 = 1 + RndObject.Next(32000);
            }

            //
            // Process S1, S2
            //
            k = rndbases1 / 53668;
            rndbases1 = 40014 * (rndbases1 - k * 53668) - k * 12211;
            if (rndbases1 < 0)
            {
                rndbases1 = rndbases1 + 2147483563;
            }
            k = rndbases2 / 52774;
            rndbases2 = 40692 * (rndbases2 - k * 52774) - k * 3791;
            if (rndbases2 < 0)
            {
                rndbases2 = rndbases2 + 2147483399;
            }

            //
            // Result
            //
            result = rndbases1 - rndbases2;
            if (result < 1)
            {
                result = result + 2147483562;
            }
            return result;
        }

        public static int rndintegermax()
        {
            int result = 0;

            result = rndbasemax;
            return result;
        }

        public static void rndinitialize(int s1,
            int s2)
        {
            rndbases1 = s1 % (rndbasem1 - 1) + 1;
            rndbases2 = s2 % (rndbasem2 - 1) + 1;
        }

    }

    enum TokenType
    {
        Empty, Id, LeftBracket, RightBracket, Number, AssignOp, AddOp, SubOp, MulOp, DivOp, ModOp,
        UnaryMinus, PowOp, AndOp, OrOp, NotOp, Colon
    }

    struct Token
    {
        public TokenType Type;
        public double Value;
        public uint qc;
        public string Lexeme;
    }

    internal class LexAnalyzer
    {

        private string _Text;
        private int i1 = 0, i2 = 0;

        private const int BufSize = 20;
        private ArrayList BufToken = new ArrayList(BufSize);
        private int CurTok = 0;

        static System.Globalization.NumberFormatInfo ni = null;

        public LexAnalyzer()
        {
            System.Globalization.CultureInfo ci = System.Globalization.CultureInfo.InstalledUICulture;

            ni = (System.Globalization.NumberFormatInfo)ci.NumberFormat.Clone();
            ni.NumberDecimalSeparator = ".";

        }

        public LexAnalyzer(string Text)
        {
            System.Globalization.CultureInfo ci = System.Globalization.CultureInfo.InstalledUICulture;
            ni = (System.Globalization.NumberFormatInfo)ci.NumberFormat.Clone();
            ni.NumberDecimalSeparator = ".";

            _Text = Text;
            FillBuffer();
        }

        public string Text
        {
            get
            {
                return _Text;
            }
            set
            {
                if (_Text != value)
                {
                    _Text = value;
                    i1 = i2 = 0;
                    BufToken.Clear();
                    FillBuffer();
                }
                CurTok = 0;
            }
        }

        public void Start()
        {
            CurTok = 0;
        }

        private void ShiftIterators(ref int iter1, ref int iter2)
        {
            iter1 = ++iter2;
        }

        private TokenType GetTokenType(char FirstCharOfToken)
        {
            int State = -1;

            if (IsAlpha(FirstCharOfToken)) State = 1;
            if (IsDigit(FirstCharOfToken)) State = 2;
            if (IsDelim(FirstCharOfToken)) State = 3;

            switch (State)
            {
                case 1:
                    return TokenType.Id;
                    break;

                case 2:
                    return TokenType.Number;
                    break;

                case 3:
                    switch (FirstCharOfToken)
                    {
                        case '+':
                            return TokenType.AddOp;
                        case '-':
                            return TokenType.SubOp;
                        case '*':
                            return TokenType.MulOp;
                        case '/':
                            return TokenType.DivOp;
                        case '(':
                            return TokenType.LeftBracket;
                        case ')':
                            return TokenType.RightBracket;
                        case '^':
                            return TokenType.PowOp;
                        case '=':
                            return TokenType.AssignOp;
                        case '|':
                            return TokenType.OrOp;
                        case '&':
                            return TokenType.AndOp;
                        case '!':
                            return TokenType.NotOp;
                        case '%':
                            return TokenType.ModOp;
                        case ':':
                            return TokenType.Colon;
                    }
                    break;

            }
            return TokenType.Empty;
        }

        private bool IsEnd()
        {
            if (_Text.Length <= i2)
                return true;
            else
                return false;
        }

        private void SkipWhitespace()
        {
            if (!IsEnd())
                while (IsWhite(_Text[i2]))
                {
                    i2++;
                    if (IsEnd())
                        break;
                }

            i2--;
            ShiftIterators(ref i1, ref i2);
        }

        public TokenType NextToken()
        {
            return NextToken(1);
        }

        public TokenType NextToken(int n)
        {
            return ((Token)(BufToken[CurTok + n - 1])).Type;
        }

        public void FillBuffer()
        {
            Token t;
            do
            {
                t = MatchToken();
                BufToken.Add(t);
            } while (t.Type != TokenType.Empty);
        }

        public Token GetToken()
        {
            if (CurTok < BufToken.Count)
            {
                return (Token)BufToken[CurTok++];
            }
            else
            {
                return (Token)BufToken[BufToken.Count - 1];
            }

        }

        public Token MatchToken()
        {
            Token t = new Token();

            if (_Text.Length == 0)
            {
                t.Type = TokenType.Empty;
                return t;
            }

            SkipWhitespace();
            if (IsEnd())
            {
                t.Type = TokenType.Empty;
                return t;
            }

            switch (t.Type = GetTokenType(_Text[i1]))
            {
                case TokenType.Id:
                    while (IsAlpha(_Text[i2]) || IsDigit(_Text[i2]))
                    {
                        i2++;
                        if (IsEnd())
                        {
                            break;
                        }
                    }
                    i2--; // <- 1 char
                    break;
                case TokenType.Number:
                    while (IsDigit(_Text[i2]))
                    {
                        i2++;
                        if (IsEnd())
                        {
                            break;
                        }
                    }
                    if (!IsEnd() && _Text[i2] == '.')
                    {
                        i2++;
                        while (IsDigit(_Text[i2]))
                        {
                            i2++;
                            if (IsEnd())
                            {
                                break;
                            }
                        }
                    }
                    i2--; // <- 1 char
                    break;
                case TokenType.LeftBracket:
                case TokenType.RightBracket:
                case TokenType.AddOp:
                case TokenType.SubOp:
                case TokenType.MulOp:
                case TokenType.DivOp:
                case TokenType.ModOp:
                case TokenType.PowOp:
                case TokenType.AssignOp:
                case TokenType.AndOp:
                case TokenType.OrOp:
                case TokenType.NotOp:
                case TokenType.Colon:

                    //
                    break;

                default:
                    t.Lexeme = "Error token!";
                    t.Type = TokenType.Empty;

                    break;

            }

            t.Lexeme = _Text.Substring(i1, i2 - i1 + 1);

            if (t.Type == TokenType.Number)
                t.Value = Convert.ToDouble(t.Lexeme, ni);
            //Convert.ToDouble(

            ShiftIterators(ref i1, ref i2);

            return t;
        }

        public void Test() { }

        // internal routines

        private bool IsWhite(char c)
        {
            return c == 32 /* space */ || c == 9 /* tab */ || c == 13 /* LF */ || c == 10 /* CR */;
        }

        private bool IsAlpha(char c)
        {
            return Char.IsLetter(c) || (c == '_');
        }

        private bool IsDigit(char c)
        {
            return Char.IsDigit(c);
        }

        private bool IsDelim(char c)
        {
            string Delimiters = "+-/*()^=&!|%:";
            return Delimiters.Contains(c.ToString());
        }
    }

    internal class VarTable
    {
        private Hashtable VarTab = new Hashtable(100);

        public VarTable() { Add("TRUE", 1); Add("FALSE", 0); Add("ИСТИНА", 1); Add("ЛОЖЬ", 0); }

        public void Print()
        {
            bool b = false;
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine("{0,30} {1,15} {2,5}", "Переменная", "Значение", "Обращений");
            Console.WriteLine("\t\t\t----------------------------------------");
            Console.ForegroundColor = ConsoleColor.Black;
            foreach (DictionaryEntry o in VarTab)
            {
                if (b = !b) Console.ForegroundColor = ConsoleColor.DarkGray;
                else Console.ForegroundColor = ConsoleColor.Gray;
                Console.WriteLine("{0,30} {1,15} {2,5}", ((Token)o.Value).Lexeme, ((Token)o.Value).Value, ((Token)o.Value).qc);
            }
            Console.ForegroundColor = ConsoleColor.Black;
        }

        public void Add(string Name, double Value)
        {
            Token a = new Token();
            a.Lexeme = Name;
            a.Value = Value;
            a.Type = TokenType.Id;

            Add(a);
        }

        public void Add(Token t)
        {
            if (!Exist(t.Lexeme))
                VarTab.Add(t.Lexeme, t);
        }

        public bool Exist(string Name)
        {
            return (VarTab[Name] != null);
        }

        public Token Get(string Name)
        {
            if (!Exist(Name))
                Add(Name, 0);

            Token t = (Token)VarTab[Name];
            t.qc++;
            VarTab[Name] = t;
            // ---------------- псевдо-функции ------------
            if (t.Lexeme == "rnd")
                t.Value = RandGen.RndObject.NextDouble();//RandGen.rndnormal();
            if (t.Lexeme == "rndnorm")
                t.Value = RandGen.rndnormal();
            // --------------------------------------------
            return t;
        }

        public void Set(string Name, double Value)
        {
            Token t = Get(Name);
            t.Value = Value;
            //t.qc++;
            VarTab[Name] = t;
        }
    }

    class CodeStack
    {
        private Stack Code = new Stack();

        public Array ToArray()
        {
            return Code.ToArray();
        }

        public void Push(Token o)
        {
            Code.Push(o);
        }

        public Token Pop()
        {
            return (Token)Code.Pop();
        }

        public string Trace()
        {
            Stack s = (Stack)Code.Clone();
            StringBuilder Result = new StringBuilder();
            //Result.AppendLine(String.Format("{0}:", s.Count));
            while (s.Count > 0)
            {
                Token t = (Token)s.Pop();
                Result.AppendFormat("{0}", t.Lexeme);
            }

            return Result.ToString();
        }
    }

    /*
     *TODO:
     * 
     *Написать дерево синтаксического разбора
     * 
     * 
     */

    class CodeProcessor
    {
        private ArrayList Code = new ArrayList();
        private ArrayList MainCode = new ArrayList();

        public VarTable VarTab;

        public CodeProcessor()
        {
        }

        public CodeProcessor(Array A)
        {
            foreach (object v in A)
                Code.Add(v);
            Code.Reverse();

            MainCode = new ArrayList(Code);
        }

        public void Clear()
        {
            Code.Clear();
            MainCode.Clear();
        }

        public string Trace()
        {
            StringBuilder Result = new StringBuilder();
            foreach (object v in MainCode)
            {
                Token t = (Token)v;
                Result.AppendFormat("{0} ", t.Lexeme);
            }
            return Result.ToString();
        }


        public int Add(Token t)
        {
            return Code.Add((Token)t);
        }

        public void Push(Token t)
        {
            Add(t);
        }

        public Token Get(int index)
        {
            Token t = (Token)Code[index];
            if (t.Type == TokenType.Id)
                t.Value = VarTab.Get(t.Lexeme).Value;
            return t;
        }
        public int Size()
        {
            return Code.Count;
        }

        public double Eval()
        {
            MainCode = new ArrayList(Code);
            //Console.WriteLine( Trace() );
            while (Size() > 1)
            {
                int i = 0;
                Token t2;
                Token t1;
                TokenType ttype;
                while (true)
                {
                    if (Size() == 1 || i >= Size())
                        break; ;
                    switch (ttype = Get(i).Type)
                    {
                        case TokenType.AddOp:
                        case TokenType.SubOp:
                        case TokenType.MulOp:
                        case TokenType.DivOp:
                        case TokenType.ModOp:
                        case TokenType.PowOp:
                        case TokenType.OrOp:
                        case TokenType.AndOp:
                        case TokenType.AssignOp:
                        case TokenType.Colon:
                            t2 = Get(i - 2);
                            t1 = Get(i - 1);

                            switch (ttype)
                            {
                                case TokenType.OrOp: t2.Value = Convert.ToDouble(Convert.ToBoolean(t2.Value) || Convert.ToBoolean(t1.Value)); break;
                                case TokenType.AndOp: t2.Value = Convert.ToDouble(Convert.ToBoolean(t2.Value) && Convert.ToBoolean(t1.Value)); break;
                                case TokenType.AddOp: t2.Value = t2.Value + t1.Value; break;
                                case TokenType.SubOp: t2.Value = t2.Value - t1.Value; break;
                                case TokenType.MulOp: t2.Value = t2.Value * t1.Value; break;
                                case TokenType.Colon: t2.Value = Math.Round(t2.Value, (int)(t1.Value > 15 ? 15 : t1.Value < 0 ? 0 : t1.Value));
                                    break;
                                case TokenType.DivOp:
                                    try
                                    {
                                        t2.Value = t2.Value / t1.Value;
                                    }
                                    catch (DivideByZeroException e)
                                    {

                                    }
                                    break;
                                case TokenType.ModOp: t2.Value = t2.Value % t1.Value; break;
                                case TokenType.AssignOp: t2.Value = t1.Value; VarTab.Set(t2.Lexeme, t2.Value); break;
                                case TokenType.PowOp: t2.Value = Math.Pow(t2.Value, t1.Value); break;
                            }

                            t2.Type = TokenType.Number;
                            Code[i - 2] = t2;
                            Code.RemoveRange(i - 1, 2);
                            i = 0;
                            break;

                        case TokenType.UnaryMinus:
                            t1 = Get(i - 1);
                            t1.Value = -t1.Value;
                            t1.Type = TokenType.Number;
                            Code[i - 1] = t1;
                            Code.RemoveAt(i);
                            i = 0;
                            break;
                        case TokenType.NotOp:
                            t1 = Get(i - 1);
                            t1.Value = Convert.ToDouble(!Convert.ToBoolean(t1.Value));
                            t1.Type = TokenType.Number;
                            Code[i - 1] = t1;
                            Code.RemoveAt(i);
                            i = 0;
                            break;

                        default:
                            i++;
                            break;
                    }
                }
            }
            if (Size() == 1)
            {
                double r = Get(0).Value;
                Code = new ArrayList(MainCode);
                return r;
            }
            else
                return -1;
        }
    }

    internal class SynAnalyzer
    {
        private LexAnalyzer Lex;
        private CodeProcessor Code;
        public bool Compiled = false;

        public SynAnalyzer()
        {

        }

        public SynAnalyzer(LexAnalyzer L, CodeProcessor C)
        {

            Lex = L;
            Code = C;
        }

        public LexAnalyzer LexicalAnalyzer
        {
            get
            {
                return Lex;
            }
            set
            {
                Lex = value;
            }
        }

        public CodeProcessor CodeList
        {
            get
            {
                return Code;
            }
            set
            {
                Code = value;
            }
        }

        public bool Parse()
        {
            Compiled = true;
            AExpr();
            return Compiled;
        }

        public void AExpr()
        {
            Token t;

            if (Lex.NextToken() == TokenType.Id && Lex.NextToken(2) == TokenType.AssignOp)
            {
                t = Lex.GetToken();
                Code.Push(t);
                t = Lex.GetToken();
                Expr();
                Code.Push(t);
            }
            else
                Expr();
        }

        public void Expr()
        {
            Token t;
            if (Lex.NextToken() == TokenType.Id && Lex.NextToken(2) == TokenType.AssignOp)
            {
                AExpr();
            }
            else
            {
                Term();
                while (true)
                {
                    switch (Lex.NextToken())
                    {
                        case TokenType.AddOp:
                        case TokenType.SubOp:
                        case TokenType.OrOp:
                        case TokenType.AndOp:
                        case TokenType.AssignOp:
                            t = Lex.GetToken();
                            Term();
                            Code.Push(t);
                            continue;
                        default:
                            return;
                    }
                }
            }
        }

        public void Term()
        {
            Token t;
            Factor();
            while (true)
            {
                switch (Lex.NextToken())
                {
                    case TokenType.MulOp:
                    case TokenType.DivOp:
                    case TokenType.PowOp:
                    case TokenType.ModOp:
                        t = Lex.GetToken();
                        Factor();
                        Code.Push(t);
                        continue;
                    default:
                        return;
                }
            }
        }
        public void FactorRounded()
        {
            if (Lex.NextToken() == TokenType.Colon)
            {
                Token tt = Lex.GetToken();
                Factor();
                Code.Push(tt);
            }
        }
        public void Factor()
        {
            Token t;

            switch (Lex.NextToken())
            {
                case TokenType.LeftBracket:
                    Lex.GetToken(); // '('
                    Expr();
                    Lex.GetToken(); // ')'
                    FactorRounded();
                    break;
                case TokenType.SubOp:
                    t = Lex.GetToken();
                    t.Type = TokenType.UnaryMinus;
                    Factor();
                    Code.Push(t);
                    break;
                case TokenType.NotOp:
                    t = Lex.GetToken();
                    t.Type = TokenType.NotOp;
                    Factor();
                    Code.Push(t);
                    break;
                case TokenType.Id:
                case TokenType.Number:
                    t = Lex.GetToken();
                    Code.Push(t);
                    FactorRounded();
                    break;
                case TokenType.Empty:
                    break;
                default:
                    Compiled = false;
                    break;
            }
        }
    }

    //--------------------------------------------
    public class Node<Type>
    {
        private Type data;
        private Node<Type> left, right;

        #region Constructors
        public Node() { }
        public Node(Type data) : this(data, null, null) { }
        public Node(Type data, Node<Type> left, Node<Type> right)
        {
            this.data = data;
            this.left = left;
            this.right = right;
        }
        #endregion

        #region Public Properties
        public Type Value
        {
            get
            {
                return data;
            }
            set
            {
                data = value;
            }
        }

        public Node<Type> Left
        {
            get
            {
                return left;
            }
            set
            {
                left = value;
            }
        }

        public Node<Type> Right
        {
            get
            {
                return right;
            }
            set
            {
                right = value;
            }
        }
        #endregion
    }

    public class BinaryTree<Type>
    {
        private Node<Type> root;

        public BinaryTree()
        {
            root = null;
        }

        #region Public Methods
        public virtual void Clear()
        {
            root = null;
        }
        #endregion

        #region Public Properties
        public Node<Type> Root
        {
            get
            {
                return root;
            }
            set
            {
                root = value;
            }
        }
        #endregion
    }

    class ExprTree
    {
        public BinaryTree<Token> tree = new BinaryTree<Token>();

        public double Eval(Node<Token> n)
        {
            switch (n.Value.Type)
            {
                case TokenType.AddOp:
                    return Eval(n.Right) + Eval(n.Left);
                    break;
                case TokenType.Number:
                    return n.Value.Value;
                    break;
                default:
                    return 0;
            }
        }
    }
    class TTT
    {
        public void t()
        {
            Node<double> n = new Node<double>();
        }
    }

    class ExprBuilder
    {
        private LexAnalyzer Lex;
        private VarTable VarTab;
        private CodeProcessor Code;
        private SynAnalyzer Syn;

        public bool IsParsed = false;

        private string _Expression;

        public ExprBuilder()
        {
            Lex = new LexAnalyzer();
            VarTab = new VarTable();
            Code = new CodeProcessor();
            RandGen.rndinitialize(DateTime.Now.Millisecond, DateTime.Now.Second);
            Code.VarTab = VarTab;
            VarTab.Add("pi", 3.14159);
            Syn = new SynAnalyzer(Lex, Code);
        }

        public ExprBuilder(string Expression)
        {
            Lex.Text = Expression;
        }

        public bool Parse()
        {
            if (!IsParsed)
            {
                Lex.Start();
                Code.Clear();
                IsParsed = Syn.Parse();
            }
            return IsParsed;
        }

        public double Result()
        {
            Parse();
            //Console.WriteLine(IsParsed.ToString());
            return IsParsed ? Code.Eval() : -1;
        }

        public string Expression
        {
            get
            {
                return _Expression;
            }
            set
            {
                if (_Expression != value)
                {
                    IsParsed = false;
                    _Expression = value;
                    Lex.Text = _Expression;
                }
            }
        }
        public string TraceCodeList()
        {
            return Code.Trace();
        }

        public void PrintVarTab()
        {
            VarTab.Print();
        }
    }

    class Program
    {
        private const string Ver = "0.01";
        private const ConsoleColor cc = ConsoleColor.White;

        static void ShowHelp()
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Expresso (build {0})", Ver);
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine("Petrov A.A.");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("-------------------------------------------------------------------------------");
            Console.WriteLine("Console utility for calculating simple arithmetic expressions.");
            Console.WriteLine("Compatible operators:");
            Console.WriteLine("  arithmetic operators: +, -, *, /, ^ - degree, % - mod, : - round");
            Console.WriteLine("  boolean operators: | - or, & - and, ! - not");
            Console.WriteLine("  assign operator <variable>=<expression>");
            Console.WriteLine("  builtin fucntions: rnd, rndnorm");
            Console.WriteLine("Type of all variables is double");
            Console.WriteLine("All variables have dafult value 0");
            Console.WriteLine("commands:");
            Console.WriteLine("  trace - print expression list in postfix form");
            Console.WriteLine("  log   - write calculations to file ");
            Console.WriteLine("  clear - console clear ");
            Console.WriteLine("  exp   - display numbers in exponential form ");
            Console.WriteLine("  exit  - close program");
            Console.WriteLine("  help  - help!");
            Console.ForegroundColor = ConsoleColor.White;
        }
        public static void ShowLogo()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\\~~~~~~~~/\n \\******/))\n  \\****/   \n   ----");
            Console.ForegroundColor = ConsoleColor.Black;
        }
        public static void Write(string s, ConsoleColor c)
        {
            Console.ForegroundColor = c;
            Console.Write(s);
            Console.ForegroundColor = cc;
        }
        public static void Write(string s)
        {
            Write(s, cc);
        }
        public static void WriteLine(string s, ConsoleColor c)
        {
            Write(s + "\n", c);
        }
        public static void WriteLine(string s)
        {
            Write(s + "\n");
        }

        public static QueryPerfCounter myTimer = new QueryPerfCounter();
        private static ExprBuilder e = new ExprBuilder();
        static void Main(string[] args)
        {
            //QueryPerfCounter myTimer = new QueryPerfCounter();
            myTimer.Start();
            myTimer.Stop();
            myTimer.Duration(1);


            bool q = false, trace = false, log = false, exp = false, batch_mode = false;

            double result;
            StreamWriter log_file = File.CreateText("expresso.log");
            StreamReader expr_file = null;

            ShowLogo();
            ShowHelp();

            do
            {

                if (!batch_mode)
                {
                    Write("# ", ConsoleColor.Green);
                    e.Expression = Console.ReadLine();
                    if (string.IsNullOrEmpty(e.Expression))
                        continue;
                }
                if (e.Expression[0] == '@')
                {
                    if (File.Exists(e.Expression.Substring(1)))
                    {
                        expr_file = File.OpenText(e.Expression.Substring(1));
                        e.Expression = "open";

                    }
                    else
                        WriteLine("File not found!");
                }
                switch (e.Expression)
                {
                    case "cls":
                    case "clear":
                        Console.Clear();
                        break;
                    case "exp":
                        exp = !exp;
                        Console.ForegroundColor = ConsoleColor.Magenta;
                        Console.WriteLine("exp={0}", exp.ToString());
                        Console.ForegroundColor = ConsoleColor.White;
                        break;
                    case "logo":
                        ShowLogo();
                        break;
                    case "var":
                        e.PrintVarTab();
                        break;
                    case "open":
                        batch_mode = true;
                        if (!expr_file.EndOfStream)
                        {
                            e.Expression = expr_file.ReadLine();
                            goto default;
                        }
                        else
                        {
                            expr_file.Close();
                            batch_mode = false;
                        }
                        break;
                    case "log":
                        log = !log;
                        Console.ForegroundColor = ConsoleColor.Magenta;
                        Console.WriteLine("log={0}", log.ToString());
                        Console.ForegroundColor = ConsoleColor.White;
                        break;
                    case "exit":
                        Write("Quit program (y | n)? ", ConsoleColor.DarkRed);
                        if (Console.ReadKey().KeyChar.ToString().ToLower() == "y")
                            q = true;
                        Console.WriteLine();
                        break;
                    case "help":
                        ShowHelp();
                        break;
                    case "trace":
                        trace = !trace;
                        Console.ForegroundColor = ConsoleColor.Magenta;
                        Console.WriteLine("trace={0}", trace.ToString());
                        Console.ForegroundColor = ConsoleColor.White;
                        break;
                    default:
                        Write("# ", ConsoleColor.Green);

                        Console.ForegroundColor = ConsoleColor.DarkCyan;
                        myTimer.Start();

                        try
                        {
                            if (!e.Parse())
                                throw new Exception();

                            if (batch_mode)
                            {
                                if (!exp)
                                    Console.Write("{0} = {1}", e.Expression, e.Result());
                                else
                                    Console.Write("{0} = {0:E}", e.Expression, e.Result());
                            }
                            else
                            {
                                if (!exp)
                                    Console.Write("{0}", e.Result());
                                else
                                    Console.Write("{0:E}", e.Result());
                            }
                        }
                        catch
                        {
                            WriteLine("Syntax error!", ConsoleColor.Red);
                        }
                        myTimer.Stop();

                        Console.ForegroundColor = ConsoleColor.White;

                        result = myTimer.Duration(1);

                        if (trace)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.Write(" | ");
                            Console.Write(e.TraceCodeList());
                            Console.ForegroundColor = ConsoleColor.White;
                            Console.Write(" | ");
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.Write("{0}ms", result / 1000000);
                            Console.ForegroundColor = ConsoleColor.Black;
                        }
                        if (log)
                        {
                            log_file.WriteLine("{0}={1}", e.Expression, e.Result());
                        }

                        Console.WriteLine();
                        if (batch_mode)
                            e.Expression = "open";
                        continue;
                }
            } while (!q);
            log_file.Close();
        }
    }

}
