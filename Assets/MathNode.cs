using System;
using System.ComponentModel;

public class MathNode
{
    private readonly NodeType _nodeType;

    private readonly string _variableName;
    private readonly int _value;

    private readonly MathNode _left;
    private readonly MathNode _right;

    public MathNode(NodeType nodeType, int value, string variableName, MathNode left = null, MathNode right = null)
    {
        _nodeType = nodeType;
        _value = value;
        _variableName = variableName;
        _left = left;
        _right = right;
    }

    public MathNode(NodeType nodeType, int value)
    {
        if (nodeType != NodeType.Value)
            throw new ArgumentException("Constructor should only be used with a value NodeType");
        _nodeType = nodeType;
        _value = value;
    }

    public MathNode(NodeType nodeType, string variableName)
    {
        if (nodeType != NodeType.Variable)
            throw new ArgumentException("Constructor should only be used with a variable NodeType");
        _nodeType = nodeType;
        _variableName = variableName;
    }

    public MathNode(NodeType nodeType, MathNode left)
    {
        if (nodeType != NodeType.Ln && nodeType != NodeType.UnaryMinus)
            throw new ArgumentException("Constructor should only be used with a Ln or UnaryMinus NodeType");
        _nodeType = nodeType;
        _left = left;
    }

    public MathNode(NodeType nodeType, MathNode left, MathNode right)
    {
        if (nodeType != NodeType.Add && nodeType != NodeType.Subtract && nodeType != NodeType.Multiply && nodeType != NodeType.Divide && nodeType != NodeType.Power)
            throw new ArgumentException("Constructor should only be used with a NodeType that takes two arguments");
        
        if (nodeType == NodeType.Divide && right._nodeType == NodeType.Value && right._value == 0)
            throw new ArgumentException("Cannot divide by 0");
        
        _nodeType = nodeType;
        _left = left;
        _right = right;
    }

    public override string ToString()
    {
        switch (_nodeType)
        {
            case NodeType.Value:
                return _value.ToString();
            case NodeType.Variable:
                return _variableName;
            case NodeType.UnaryMinus:
                return "-(" + _left + ")";
            case NodeType.Ln:
                return "ln(" + _left + ")";
            default:
                return "(" + _left + ")" + _nodeType.GetDescription() + "(" + _right + ")";
        }
    }

    public static MathNode Derivative(MathNode node)
    {
        if (node == null) return null;
        
        switch (node._nodeType)
        {
            case NodeType.Value:
                return new MathNode(NodeType.Value, 0);
            
            case NodeType.Variable:
                return new MathNode(NodeType.Value, 1);
            
            case NodeType.UnaryMinus:
                return new MathNode(NodeType.UnaryMinus, Derivative(node));
            
            case NodeType.Add:
            case NodeType.Subtract:
                return new MathNode(node._nodeType, Derivative(node._left), Derivative(node._right));
            
            case NodeType.Multiply:
                return new MathNode(NodeType.Add,
                    new MathNode(NodeType.Multiply, Derivative(node._left), node._right),
                    new MathNode(NodeType.Multiply, node._left, Derivative(node._right))
                );
            
            case NodeType.Divide:
                return new MathNode(NodeType.Divide,
                    new MathNode(NodeType.Subtract,
                        new MathNode(NodeType.Multiply, Derivative(node._left), node._right),
                        new MathNode(NodeType.Multiply, node._left, Derivative(node._right))
                    ),
                    new MathNode(NodeType.Power, node._right, new MathNode(NodeType.Value, 2))
                );
            
            case NodeType.Power:
                return new MathNode(NodeType.Multiply,
                    new MathNode(NodeType.Multiply,
                        node._right,
                        new MathNode(NodeType.Power, 
                            node._left, 
                            new MathNode(NodeType.Subtract, 
                                node._right, 
                                new MathNode(NodeType.Value, 1)
                                )
                        )
                    ),
                    Derivative(node._left)
                );
            
            case NodeType.Ln:
                return new MathNode(NodeType.Divide,
                    Derivative(node._left),
                    node._left
                );
            
            default:
                throw new ArgumentException("Unexpected NodeType '" + node._nodeType.GetDescription() +"'");
        }
    }

    public static double SolveForValue(MathNode node, float value)
    {
        if (node == null) throw new ArgumentException("Node should not be null");
        switch (node._nodeType)
        {
            case NodeType.Value:
                return node._value;
            case NodeType.Variable:
                return value;
            case NodeType.UnaryMinus:
                return -SolveForValue(node._left, value);
            case NodeType.Add:
                return SolveForValue(node._left, value) + SolveForValue(node._right, value);
            case NodeType.Subtract:
                return SolveForValue(node._left, value) - SolveForValue(node._right, value);
            case NodeType.Multiply:
                return SolveForValue(node._left, value) * SolveForValue(node._right, value);
            case NodeType.Divide:
                return SolveForValue(node._left, value) / SolveForValue(node._right, value);
            case NodeType.Power:
                return Math.Pow(SolveForValue(node._left, value), SolveForValue(node._right, value));
            case NodeType.Ln:
                return Math.Log(SolveForValue(node._left, value));
            default:
                throw new ArgumentException("Unexpected NodeType '" + node._nodeType.GetDescription() + "'");
        }
    }
}

public enum NodeType
{
    [Description("value")] Value,
    [Description("variable")] Variable,
    [Description("-")] UnaryMinus,
    [Description("+")] Add,
    [Description("-")] Subtract,
    [Description("*")] Multiply,
    [Description("/")] Divide,
    [Description("^")] Power,
    [Description("Ln")] Ln
}
