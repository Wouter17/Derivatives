using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using UnityEngine;
using Object = UnityEngine.Object;
using IEnumerator = System.Collections.IEnumerator;

public class derivativesScript : MonoBehaviour
{

	public KMAudio audio;
	public KMBombInfo bomb;
	public KMSelectable[] keypad;
	public TextMesh equationText;
	public TextMesh screen;

	private List<string> _equations = new List<string>();
	private List<string> _solutions = new List<string>();
	private int _solvesNeeded = 1;
	private int _currentEquation;
	private bool _error;

	//settings 
	public int maxEquations = 10;
	public int wildcardChance = 10;
	public readonly int[][] ranges =
	{
		new []{-19,20},
		new []{1,2,4,8,16,32},
		new []{1,2,4},
		new []{-99,100}, //was used for z in log(z*x^y)
	    new []{0,10},
		new []{-10,10}
	};

	//logging
	static int moduleIdCounter = 1;
	int moduleId;
	private bool moduleSolved;

	void Awake()
	{
		moduleId = moduleIdCounter++;
		foreach (var key in keypad) {
			var pressedKey = key;
			key.OnInteract += delegate () { KeypadPress(pressedKey); return false; };
		}

	}

	void Start()
	{
		var time = bomb.GetTime();
		_solvesNeeded = Math.Min((int)Math.Ceiling(time / 180), maxEquations);
		Debug.LogFormat("[Derivatives #{0}] Generating {1} equations", moduleId, _solvesNeeded);
		GenerateEquations(_solvesNeeded);
		GenerateSolutions();
		SetEquationText("y = " + _equations[_currentEquation]);
	}

	void KeypadPress(KMSelectable key)
	{
		if (_error) ModuleSolve();
		if (moduleSolved) { return; }

		key.AddInteractionPunch();
		audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, key.transform);

		if (key.name.EndsWith("solve"))
		{
			checkSolve();
		} else if (key.name.EndsWith("del"))
		{
			deleteCharacter();
		}
		else
		{
			addCharacter(key);
		}
	}

	void deleteCharacter()
	{
		if (screen.text.Length > 8) SetScreenText(screen.text.Remove(screen.text.Length - 1));
	}

	void addCharacter(Object key)
	{
		SetScreenText(screen.text + key.name.Last());
	}

	void GenerateEquations(int amount)
	{
		for (var i = 0; i < amount; i++)
		{
			var additions = UnityEngine.Random.Range(1, 4);
			var equation = "";
			for (var x = 0; x < additions; x++)
			{
				var z = 0;
				var wildcard = "";
				var numbers = new int[ranges.Length];
				foreach (var range in ranges)
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
					wildcard = UnityEngine.Random.Range(0, 2) == 0 ? string.Format(" + log(x^{0})", numbers[4]) : string.Format(" * x^{0}", numbers[5]);
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
		Debug.LogFormat("[Derivatives #{0}] The equations are:\n{1}", moduleId, _equations.Join("\n"));
	}

	static string PlusMinus(bool emptyOnTrue = false)
	{
		if (emptyOnTrue)
		{
			return UnityEngine.Random.Range(0, 2) == 0 ? "" : "-";
		}
		return UnityEngine.Random.Range(0, 2) == 0 ? "+" : "-";
	}

	void nextEquation()
	{
		_currentEquation++;
		SetEquationText("y = " + _equations[_currentEquation]);
		SetScreenText("dy/dx = ");
	}

	void SetEquationText(string text)
	{
		equationText.text = text;
		equationText.characterSize = (0.17f - equationText.text.Length * 0.00186f) * 0.25f;
	}

	void SetScreenText(string text)
	{
		screen.text = text;
		screen.characterSize = 0.0575f * (float)Math.Pow(0.98, screen.text.Length);
	}

	void GenerateSolutions()
	{
		var httpWebRequest = (HttpWebRequest)WebRequest.Create("http://api.mathjs.org/v4/");
		httpWebRequest.ContentType = "application/json";
		httpWebRequest.Method = "POST";

		using (var streamWriter = new StreamWriter(httpWebRequest.GetRequestStream()))
		{
			var equations = _equations.Select(x => string.Format("derivative('{0}', 'x')", x));
			var json = "{\"expr\": [\"" + equations.Join("\",\"") + "\"]}";
			//var json = "{\"expr\": [\"derivative('2x^3', 'x')\", \"derivative('7x^2', 'x')\", \"derivative('9x^8', 'x')\"]}";
			streamWriter.Write(json);
		}
		try
		{
			var httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();

			using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
			{
				var result = streamReader.ReadToEnd();
				var answersList = Regex.Matches(result, @"\[(.*?)\]")[0].ToString();
				_solutions = answersList
					.Substring(1, answersList.Length - 2)
					.Split(',')
					.Select(x => x.Substring(1, x.Length - 2))
					.ToList();
			}
		}
		catch (Exception e)
		{
			Console.WriteLine(e);
			handleConnectionError(e);
			throw;
		}
		Debug.LogFormat("[Derivatives #{0}] The solutions are:\n{1}", moduleId, _solutions.Join("\n"));
	}

	void handleConnectionError(Exception error)
	{
		_error = true;
		SetEquationText(error.Message);
		SetScreenText("Press a button to solve module");
		Debug.LogFormat("[Derivatives #{0}] Failed to get solutions, press a button to solve the module", moduleId);
	}

	void checkSolve()
	{
		if (_error) { moduleSolved = true; return; }
		var r = new Regex(@"(?<=-)\(\d*((x|\/x)\^(\d*|\((|-)\d*\/\d*)|x)\)"); //remove extra brackets in -(y/x) or -(yx)
		var r2 = new Regex(@"\(1\/x\)"); //remove brackets in 1/x
		var r3 = new Regex(@"x\^\((-| )\d*\/\d*\)-\d*\/\d*"); //replace - location in multiplication x^(-1/2)-11/2 -> -x^(-1/2)11/2

		//var answerList = Regex.Split(screen.text, @" (\+|-) /gm").ToList();
		//var solveList = Regex.Split(_solutions[_currentEquation], @" (\+|-) /gm").ToList();
		//var answerList = Regex.Replace(screen.text.Substring(8),@"(\\*|\\(|\\)| )","");
		//var solveList = Regex.Replace(_solutions[_currentEquation], @"(\*|\(|\)| )","");
		var answerList = Regex.Split(r.Replace(screen.text.Substring(8)
						.Replace("+-", "-")
					, ReplaceEquationBrackets)
				, @"(?<!(\(|\/|\*))(?=-)(?!.\()|\+")
			.Where(x => x != string.Empty)
			.ToList()
			.Select(x => r3.Replace(r2.Replace(
					x.Replace("*", "")
					, ReplaceOneDividedByX)
					, ReplaceMoveMinus)
			)
			.ToList();


		//answerList = Regex.Split(answerList, @"(?<!(\(|\/|\*))(-|\+)(?!\()")
		var solutionList = Regex.Split(_solutions[_currentEquation].Replace("+ -", "- ")
				, @"(?<= )(?=-)(?=. )|\+")
			.Where(x => x != string.Empty)
			.ToList()
			.Select(x => r3.Replace(r2.Replace(r.Replace(
					x.Replace(" ", "").Replace("*", "")
					, ReplaceEquationBrackets)
					, ReplaceOneDividedByX)
					, ReplaceMoveMinus)
			)
			.ToList();

		answerList.Sort();
		solutionList.Sort();

		if (answerList.SequenceEqual(solutionList))
		{
			Debug.LogFormat("[Derivatives #{0}] Equation {1} solved correctly", moduleId, _currentEquation + 1);

			if (_currentEquation + 1 == _solvesNeeded)
			{
				audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.CorrectChime, transform);
				ModuleSolve();
			}
			else
			{
				audio.PlaySoundAtTransform("success", transform);
				nextEquation();
			}
		}
		else
		{
			Debug.LogFormat("[Derivatives #{0}] Equation {1} answer incorrect\nexpected: {2}\n(raw): {3}\n but got: {4} \n(raw): {5}", moduleId, _currentEquation + 1, solutionList.Join(), _solutions[_currentEquation], answerList.Join(), screen.text);
			handleStrike();
		}
	}

	static string ReplaceEquationBrackets(Match match)
	{
		return match.ToString().Substring(1, match.Length - 2);
	}

	static string ReplaceOneDividedByX(Match match)
	{
		return match.ToString().Substring(2, match.Length - 3);
	}

	static string ReplaceMoveMinus(Match match)
	{
		return '-' + match.ToString().TrimEnd('-');
	}

	void handleStrike()
	{
		audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.Strike, transform);
		GetComponent<KMBombModule>().HandleStrike();
		if (_currentEquation + 1 == _solvesNeeded)
		{
			ModuleSolve();
		} else nextEquation();
	}
	void ModuleSolve()
	{
		moduleSolved = true;
		GetComponent<KMBombModule>().HandlePass();
	}

	//Twitch Plays
	#pragma warning disable 414
	private readonly string TwitchHelpMessage = @"!{0} type <answer> [Inputs the specified answer] | !{0} delete (#) [Deletes the last inputted character (optionally '#' times)] | !{0} submit/enter [Enters the current input]";
	#pragma warning restore 414

	IEnumerator ProcessTwitchCommand(string command) //Handles commands sent in via Twitch
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
				int temp = -1;
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

	IEnumerator TwitchHandleForcedSolve() //Handles autosolving the module
	{
		int start = _currentEquation;
		for (int i = start; i < _solvesNeeded; i++)
		{
			string noSpaceSolution = _solutions[i].Replace(" ", "");
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
						while (screen.text.Substring(8).Length > _solutions[i].Replace(" ", "").Length)
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
}
