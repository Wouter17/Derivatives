﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using Object = UnityEngine.Object;
using IEnumerator = System.Collections.IEnumerator;

public class DerivativesScript : MonoBehaviour
{
	//Bomb components
	public new KMAudio audio;
	public KMBombInfo bomb;
	public KMSelectable[] keypad;
	public TextMesh equationText;
	public TextMesh screen;

	//state
	private readonly List<string> _equations = new List<string>();
	private List<MathNode> _solutions = new List<MathNode>();
	private int _solvesNeeded = 1;
	private int _currentEquation;
		
	//settings 
	public Color defaultColor = new Color(99, 99, 99, 255); 
	public int maxEquations = 10;
	public int wildcardChance = 10;
	public int checkingLimit = 1000;
	public double precision = 1E-4;

	private readonly int[][] _ranges =
	{
		new[] { -19, 20 },
		new[] { 1, 2, 4, 8, 16, 32 },
		new[] { 1, 2, 4 },
		new[] { -99, 100 }, //was used for z in log(z*x^y)
		new[] { 0, 10 },
		new[] { -10, 10 }
	};

	//logging
	private static int _moduleIdCounter = 1;
	private int _moduleId;
	private bool _moduleSolved;
	
	//properties
	private static readonly int Color1 = Shader.PropertyToID("_Color");

	private void Awake()
	{
		_moduleId = _moduleIdCounter++;
			
		foreach (var key in keypad)
		{
			var pressedKey = key;
			key.OnInteract += () =>
			{
				KeypadPress(pressedKey);
				return false;
			};
		}
	}

	private void Start()
	{
		var time = bomb.GetTime();
		_solvesNeeded = Math.Min((int)Math.Ceiling(time / 180), maxEquations);

		LOG(string.Format("generating {0} equations", _solvesNeeded));
		GenerateEquations(_solvesNeeded);
		GenerateSolutions();
		SetEquationText("y = " + _equations[_currentEquation]);
	}

	private void KeypadPress(KMSelectable key)
	{
		if (_moduleSolved) return;

		key.AddInteractionPunch();
		audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, transform);

		if (key.name.EndsWith("solve"))
		{
			CheckSolve();
		}
		else if (key.name.EndsWith("del"))
		{
			DeleteCharacter();
		}
		else
		{
			AddCharacter(key);
		}
	}

	private void DeleteCharacter()
	{
		if (screen.text.Length > 8) SetScreenText(screen.text.Remove(screen.text.Length - 1));
	}

	private void AddCharacter(Object key)
	{
		SetScreenText(screen.text + key.name.Last());
	}

	private void GenerateEquations(int amount)
	{
		for (var i = 0; i < amount; i++)
		{
			var additions = UnityEngine.Random.Range(1, 4);
			string equation = null;
			for (var x = 0; x < additions; x++)
			{
				var z = 0;
				var wildcard = "";
				var numbers = new int[_ranges.Length];
				foreach (var range in _ranges)
				{
					if (range.Length > 2)
					{
						numbers[z] = range[UnityEngine.Random.Range(0, range.Length)];
					}
					else
					{
						numbers[z] = UnityEngine.Random.Range(range[0], range[1]);
					}

					z++;
				}

				if (numbers[1] >= numbers[2])
				{
					numbers[1] = numbers[1] / numbers[2];
					numbers[2] = 0;
				}

				if (UnityEngine.Random.Range(0, 100) < wildcardChance)
				{
					wildcard = UnityEngine.Random.Range(0, 2) == 0
						? string.Format(" + ln(x^{0})", numbers[4])
						: string.Format(" * x^{0}", numbers[5]); //TODO: implement wildcards
				}

				equation += string.Format("{0}{1}*x^({2}{3}{4}{5}){6} ",
					numbers[0] >= 0 && x != 0 ? "+ " : "",
					numbers[0],
					PlusMinus(true),
					numbers[1],
					numbers[2] == 0 ? "" : "/",
					numbers[2] == 0 ? (object)"" : numbers[2],
					wildcard
				);
			}

			_equations.Add(equation);
		}

		LOG(string.Format("the equations are:\n{0}", _equations.Join("\n")));
	}

	private static string PlusMinus(bool emptyOnTrue = false)
	{
		if (emptyOnTrue)
		{
			return UnityEngine.Random.Range(0, 2) == 0 ? "" : "-";
		}

		return UnityEngine.Random.Range(0, 2) == 0 ? "+" : "-";
	}

	private void NextEquation()
	{
		_currentEquation++;
		SetEquationText("y = " + _equations[_currentEquation]);
		SetScreenText("dy/dx = ");
	}

	private void SetEquationText(string text)
	{
		equationText.text = text;
		equationText.characterSize = (0.17f - text.Length * 0.00186f) * 0.25f;
	}

	private void SetScreenText(string text)
	{
		screen.text = text;
		if (text.Length < 22)
		{
			screen.characterSize = 0.04f;
			return;
		}

		screen.characterSize = (float)(0.9398f * Math.Pow(text.Length, -1.027f));
	}

	private void GenerateSolutions()
	{
		_solutions = _equations.Select(equation => MathNode.Derivative(MathNodeConverter.StringToCalculator(
			equation.Replace(" ", "")
		))).ToList();
		LOG("the solutions are:\n" + _solutions.Join("\n"));
	}

	private void CheckSolve()
	{
		var correct = true;

		var textOnScreen = screen.text.Substring(8);
		
		if (textOnScreen.Count(c => c == '(') != textOnScreen.Count(c => c == ')'))
		{
			InvalidInput();
			return;
		}
		
		MathNode answerGiven;
		try
		{
			answerGiven = MathNodeConverter.StringToCalculator(textOnScreen);
		}
		catch (Exception e)
		{
			InvalidInput();
			LOG(e.ToString());
			return;
		}
		
		var correctDerivative = _solutions[_currentEquation];

		for (int i = 1; i < checkingLimit; i++)
		{
			if (double.IsNaN(MathNode.SolveForValue(answerGiven, i)) || !NearlyEqual(
				    MathNode.SolveForValue(answerGiven, i), MathNode.SolveForValue(correctDerivative, i), precision))
			{
				correct = false;
				LOG(string.Format("equation {0} answer incorrect\nexpected: {1}\nbut got: {2}\nfor x = {3}\nfrom input: {4}\nfor equation: {5}",
						_currentEquation + 1,
						MathNode.SolveForValue(correctDerivative, i),
						MathNode.SolveForValue(answerGiven, i),
						i,
						textOnScreen,
						_equations[_currentEquation])
					);
				HandleStrike();
				break;
			}
		}


		if (correct)
		{
			LOG(string.Format("equation {0} solved correctly", _currentEquation + 1));

			if (_currentEquation + 1 == _solvesNeeded)
			{
				ModuleSolve();
			}
			else
			{
				audio.PlaySoundAtTransform("success", transform);
				NextEquation();
			}
		}
	}

	private void InvalidInput()
	{
		StartCoroutine(FlashButtonColor(Color.red));
	}

	#region Button colors

	private void SetButtonColor(Color color)
	{
		foreach (var key in keypad)
		{
			key.GetComponent<Renderer>().material.SetColor(Color1, color);
		}
	}

	private IEnumerator FlashButtonColor(Color color, float time = 0.5f)
	{
		for (int i = 0; i < 3; i++)
		{
			SetButtonColor(color);
			yield return new WaitForSeconds(time);
			SetButtonColor(defaultColor);
			yield return new WaitForSeconds(time);
		}
	}
	
	#endregion

	private static bool NearlyEqual(double a, double b, double epsilon)
	{
		const double minNormal = 2.2250738585072014E-308d;
		double absA = Math.Abs(a);
		double absB = Math.Abs(b);
		double diff = Math.Abs(a - b);

		if (a.Equals(b))
		{
			// shortcut, handles infinities
			return true;
		}
		else if (a == 0 || b == 0 || absA + absB < minNormal)
		{
			// a or b is zero or both are extremely close to it
			// relative error is less meaningful here
			return diff < (epsilon * minNormal);
		}
		else
		{
			// use relative error
			return diff / (absA + absB) < epsilon;
		}
	}

	private void HandleStrike()
	{
		audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.Strike, transform);
		GetComponent<KMBombModule>().HandleStrike();
		if (_currentEquation + 1 == _solvesNeeded)
		{
			ModuleSolve();
		}
		else NextEquation();
	}

	private void ModuleSolve()
	{
		_moduleSolved = true;
		audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.CorrectChime, transform);
		GetComponent<KMBombModule>().HandlePass();
	}

	private void LOG(string message)
	{
		var formattedMessage = message.Replace("\n", string.Format("\n[Derivatives #{0}] ", _moduleId));
		Debug.Log(string.Format("[Derivatives #{0}] " + formattedMessage, _moduleId));
	}

	#region Twitch Plays
	
	#pragma warning disable 414
	private readonly string TwitchHelpMessage = @"!{0} type <answer> [Inputs the specified answer] | !{0} delete (#) [Deletes the last inputted character (optionally '#' times)] | !{0} submit/enter [Enters the current input]";
	#pragma warning restore 414
		
	/// <summary>
	/// Handles commands sent in via Twitch
	/// </summary>
	private IEnumerator ProcessTwitchCommand(string command) 
	{
		if (Regex.IsMatch(command, @"^\s*(submit|enter)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
			yield return null;
			keypad[19].OnInteract();
        }
		string[] parameters = command.Split(' ');
		if (Regex.IsMatch(parameters[0], @"^\s*type\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
		{
			yield return null;
			if (parameters.Length == 1)
				yield return "sendtochaterror Please specify an answer to input!";
			else
            {
				parameters[1] = command.Substring(5);
				char[] validChars = { '7', '4', '1', '0', '8', '5', '2', '(', '9', '6', '3', ')', '/', '*', '-', '+', ' ', '^', 'x' };
				bool onlySpaces = true;
				for (int i = 0; i < parameters[1].Length; i++)
                {
					if (!validChars.Contains(parameters[1].ToLowerInvariant()[i]))
					{
						yield return "sendtochaterror!f The specified character '" + parameters[1][i] + "' is not typable!";
						yield break;
					}
					else if (!parameters[1][i].Equals(' ') && onlySpaces)
						onlySpaces = false;
                }
				if (onlySpaces)
				{
					yield return "sendtochaterror Please specify an answer to input!";
					yield break;
				}
				for (int i = 0; i < parameters[1].Length; i++)
				{
					if (!parameters[1][i].Equals(' '))
					{
						keypad[Array.IndexOf(validChars, parameters[1].ToLowerInvariant()[i])].OnInteract();
						yield return new WaitForSeconds(0.1f);
					}
				}
			}
		}
		if (Regex.IsMatch(parameters[0], @"^\s*delete\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
		{
			yield return null;
			if (parameters.Length > 2)
				yield return "sendtochaterror Too many parameters!";
			else if (parameters.Length == 1)
            {
				if (screen.text.Length <= 8)
                {
					yield return "sendtochaterror There are no characters that can be deleted!";
					yield break;
				}
				keypad[16].OnInteract();
			}
			else
			{
				int temp;
				if (!int.TryParse(parameters[1], out temp))
                {
					yield return "sendtochaterror!f The specified number of times to delete '" + parameters[1] + "' is invalid!";
					yield break;
				}
				if (temp < 1)
				{
					yield return "sendtochaterror The specified number of times to delete '" + parameters[1] + "' is less than 1!";
					yield break;
				}
				if (screen.text.Length <= 8)
				{
					yield return "sendtochaterror There are no characters that can be deleted!";
					yield break;
				}
				if ((screen.text.Length - temp) < 8)
				{
					yield return "sendtochaterror There is not enough characters to delete that '" + temp + "' times!";
					yield break;
				}
				for (int i = 0; i < temp; i++)
                {
					keypad[16].OnInteract();
					yield return new WaitForSeconds(0.1f);
				}
			}
		}
	}

	/// <summary>
	/// Handles autosolving the module
	/// </summary>
	IEnumerator TwitchHandleForcedSolve()
	{
		int start = _currentEquation;
		for (int i = start; i < _solvesNeeded; i++)
		{
			string noSpaceSolution = _solutions[i].ToString();
			string screenText = screen.text.Substring(8);
			if (screenText != noSpaceSolution)
			{
				int clearNum = -1;
				for (int j = 0; j < screenText.Length; j++)
				{
					if (j == noSpaceSolution.Length)
						break;
					if (screenText[j] != noSpaceSolution[j])
					{
						clearNum = j;
						int target = screenText.Length - j;
						for (int k = 0; k < target; k++)
						{
							keypad[16].OnInteract();
							yield return new WaitForSeconds(0.1f);
						}
						break;
					}
				}
				if (clearNum == -1)
                {
					if (screenText.Length > noSpaceSolution.Length)
					{
						while (screen.text.Substring(8).Length > _solutions[i].ToString().Length)
						{
							keypad[16].OnInteract();
							yield return new WaitForSeconds(0.1f);
						}
					}
					else
						yield return ProcessTwitchCommand("type " + noSpaceSolution.Substring(screenText.Length));
				}
				else
					yield return ProcessTwitchCommand("type " + noSpaceSolution.Substring(clearNum));
			}
			keypad[19].OnInteract();
		}
	}
	#endregion
}