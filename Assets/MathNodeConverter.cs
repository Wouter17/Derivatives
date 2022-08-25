using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

public static class MathNodeConverter
{
    public static MathNode StringToCalculator(string toConvert, List<string> inputParts = null)
	{
		if (string.IsNullOrEmpty(toConvert)) return new MathNode(NodeType.Value, 0);

		//var endTester = new Regex(@"^\[\d+\]$");
		var rMatchLn = new Regex(@"ln\([^()]*\)");
		var rMatchBrackets = new Regex(@"\([^()]*\)");
		var rMatchUnaryMinus = new Regex(@"(?<=(\+|-|\/|\*|\^|^))-([a-zA-Z]+|\d+|\[\d+\])(?!\^)");
		var rMatchPow = new Regex(@"([a-zA-Z]+|\d+|\[\d+\])\^([a-zA-Z]+|\d+|\[\d+\])");
		var rMatchImplicitMultiply = new Regex(@"(\d+|\[\d+\])[a-zA-Z]");
		var rMatchMultiplyDivide =
			new Regex(
				@"(([a-zA-Z]+|\d+|\[\d+\])(\*|\/)([a-zA-Z]+|\d+|\[\d+\]))|(\[\d+\]|\d+|[a-zA-Z]+)\[\d+\]"); //does accept 7(2*5) but doesn't (2*5)7
		var rMatchAddMinus = new Regex(@"([a-zA-Z]+|\d+|\[\d+\])(\+|-)([a-zA-Z]+|\d+|\[\d+\])");
		var parts = new List<string> { toConvert };
		if (inputParts != null) parts = inputParts;

		while (true)
		{
			if (rMatchLn.IsMatch(toConvert))
			{
				toConvert = rMatchLn.Replace(toConvert, match =>
				{
					parts.Add(match.ToString());
					return string.Format("[{0}]", parts.Count - 1);
				}, 1);
				continue;
			}

			if (rMatchBrackets.IsMatch(toConvert))
			{
				toConvert = rMatchBrackets.Replace(toConvert, match =>
				{
					parts.Add(match.ToString());
					return string.Format("[{0}]", parts.Count - 1);
				}, 1);
				continue;
			}

			if (rMatchUnaryMinus.IsMatch(toConvert))
			{
				toConvert = rMatchUnaryMinus.Replace(toConvert, match =>
				{
					parts.Add(match.ToString());
					return string.Format("[{0}]", parts.Count - 1);
				}, 1);
				continue;
			}

			if (rMatchPow.IsMatch(toConvert))
			{
				toConvert = rMatchPow.Replace(toConvert, match =>
				{
					parts.Add(match.ToString());
					return string.Format("[{0}]", parts.Count - 1);
				}, 1);
				continue;
			}
			if (rMatchMultiplyDivide.IsMatch(toConvert))
			{
				toConvert = rMatchMultiplyDivide.Replace(toConvert, match =>
				{
					parts.Add(match.ToString());
					return string.Format("[{0}]", parts.Count - 1);
				}, 1);
				continue;
			}
			if (rMatchImplicitMultiply.IsMatch(toConvert))
			{
				toConvert = rMatchImplicitMultiply.Replace(toConvert, match =>
				{
					parts.Add(match.ToString());
					return string.Format("[{0}]", parts.Count - 1);
				}, 1);
				continue;
			}
			if (rMatchAddMinus.IsMatch(toConvert))
			{
				toConvert = rMatchAddMinus.Replace(toConvert, match =>
				{
					parts.Add(match.ToString());
					return string.Format("[{0}]", parts.Count - 1);
				}, 1);
				continue;
			}
			break;
		}

		return PartialStringToCalculator(parts, toConvert);
	}

	private static MathNode PartialStringToCalculator(List<string> parts, string part)
	{
		var rValue = new Regex(@"^-?\d+$");
		var rSymbol = new Regex(@"^[a-zA-Z]+$");
		var rVariable = new Regex(@"^\[\d+\]$");
		var rMatchBrackets = new Regex(@"^\(.*\)$");
		var rMatchLn = new Regex(@"ln\([^()]*\)");
		var rMatchUnaryMinus = new Regex(@"^-([a-zA-Z]+|\d+|\[\d+\])$");
		var rMatchAdd = new Regex(@"^([a-zA-Z]+|\d+|\[\d+\])\+([a-zA-Z]+|\d+|\[\d+\])$");
		var rMatchMinus = new Regex(@"^([a-zA-Z]+|\d+|\[\d+\])-([a-zA-Z]+|\d+|\[\d+\])$");
		var rMatchMultiply = new Regex(@"^-?([a-zA-Z]+|\d+|\[\d+\])\*-?([a-zA-Z]+|\d+|\[\d+\])$");
		var rMatchImplicitMultiplyWithMultipleVariable = new Regex(@"^\[\d+\]\[\d+\]$");
		var rMatchImplicitMultiplyWithVariable = new Regex(@"^-?([a-zA-Z]|\d+)\*?\[\d+\]$");
		var rMatchImplicitMultiplyWithSymbol = new Regex(@"^-?(\d+|\[\d+\])\*?[a-zA-Z]+$");
		var rMatchDivide = new Regex(@"^-?([a-zA-Z]+|\d+|\[\d+\])\/-?([a-zA-Z]+|\d+|\[\d+\])$");
		var rMatchPow = new Regex(@"^([a-zA-Z]+|\d+|\[\d+\])\^([a-zA-Z]+|\d+|\[\d+\])$");


		if (rMatchLn.IsMatch(part))
		{
			return new MathNode(
				NodeType.Ln,
				StringToCalculator(part.Substring(3, part.Length - 4), parts.Take(parts.Count - 1).ToList())
			);
		}

		if (rMatchBrackets.IsMatch(part))
		{
			return StringToCalculator(part.Substring(1, part.Length - 2), parts.Take(parts.Count - 1).ToList());
		}

		if (rValue.IsMatch(part))
		{
			return new MathNode(
				NodeType.Value,
				int.Parse(part)
			);
		}

		if (rSymbol.IsMatch(part))
		{
			return new MathNode(
				NodeType.Variable,
				part
			);
		}

		if (rVariable.IsMatch(part))
		{
			var parameters = Regex.Matches(part, @"\[\d+\]").Cast<Match>()
				.Select(match => match.Value.Substring(1, match.Value.Length - 2)).ToList();
			return PartialStringToCalculator(parts, parts[int.Parse(parameters[0])]);
		}

		if (rMatchUnaryMinus.IsMatch(part))
		{
			return new MathNode(
				NodeType.UnaryMinus,
				PartialStringToCalculator(parts, part.Substring(1))
			);
		}

		if (rMatchAdd.IsMatch(part))
		{
			return new MathNode(
				NodeType.Add,
				PartialStringToCalculator(parts, part.Substring(0, part.IndexOf('+'))),
				PartialStringToCalculator(parts, part.Substring(part.IndexOf('+') + 1))
			);
		}

		if (rMatchMinus.IsMatch(part))
		{
			return new MathNode(
				NodeType.Subtract,
				PartialStringToCalculator(parts, part.Substring(0, part.IndexOf('-'))),
				PartialStringToCalculator(parts, part.Substring(part.IndexOf('-') + 1))
			);
		}

		if (rMatchMultiply.IsMatch(part))
		{
			return new MathNode(
				NodeType.Multiply,
				PartialStringToCalculator(parts, part.Substring(0, part.IndexOf('*'))),
				PartialStringToCalculator(parts, part.Substring(part.IndexOf('*') + 1))
			);
		}

		if (rMatchImplicitMultiplyWithMultipleVariable.IsMatch(part))
		{
			return new MathNode(
				NodeType.Multiply,
				PartialStringToCalculator(parts, part.Substring(0, part.IndexOf("]", StringComparison.Ordinal) + 1)),
				PartialStringToCalculator(parts, part.Substring(part.IndexOf("]", StringComparison.Ordinal) + 1))
			);
		}

		if (rMatchImplicitMultiplyWithVariable.IsMatch(part))
		{
			return new MathNode(
				NodeType.Multiply,
				PartialStringToCalculator(parts, part.Substring(0, part.IndexOf("[", StringComparison.Ordinal))),
				PartialStringToCalculator(parts, part.Substring(part.IndexOf("[", StringComparison.Ordinal)))
			);
		}

		if (rMatchImplicitMultiplyWithSymbol.IsMatch(part))
		{
			return new MathNode(
				NodeType.Multiply,
				PartialStringToCalculator(parts, Regex.Match(part, @"^-?(\d+|\[\d+\])").Value),
				PartialStringToCalculator(parts, Regex.Match(part, @"[a-zA-Z]+$").Value)
			);
		}

		if (rMatchDivide.IsMatch(part))
		{
			return new MathNode(
				NodeType.Divide,
				PartialStringToCalculator(parts, part.Substring(0, part.IndexOf('/'))),
				PartialStringToCalculator(parts, part.Substring(part.IndexOf('/') + 1))
			);
		}

		if (rMatchPow.IsMatch(part))
		{
			return new MathNode(
				NodeType.Power,
				PartialStringToCalculator(parts, part.Substring(0, part.IndexOf('^'))),
				PartialStringToCalculator(parts, part.Substring(part.IndexOf('^') + 1))
			);
		}

		throw new ArgumentException("Invalid Calculator string supplied", part);
	}
    
}