using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using UnityEngine;
using Object = UnityEngine.Object;

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

    void Awake () 
    {
        moduleId = moduleIdCounter++;
        foreach (var key in keypad){
            var pressedKey = key;
            key.OnInteract += delegate () { KeypadPress(pressedKey); return false; };
        }

    }

	void Start ()
	{
		var time = bomb.GetTime();
		_solvesNeeded = Math.Min( (int)Math.Ceiling(time/180) , maxEquations );
		Debug.LogFormat("Derivatives #{0} generating {1} equations", moduleId, _solvesNeeded);
		GenerateEquations(_solvesNeeded);
		GenerateSolutions();
		SetEquationText("y = " + _equations[_currentEquation]);
	}
	
	void KeypadPress(KMSelectable key)
	{
		if (_error) ModuleSolve();
		if(moduleSolved){ return; }
		
		key.AddInteractionPunch();
		GetComponent<KMAudio>().PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, transform);
		
		if (key.name.EndsWith("solve"))
		{
			checkSolve();
		}else if (key.name.EndsWith("del"))
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
					numbers[0] >= 0 && x!=0 ? "+ " : "",
					numbers[0],
					PlusMinus(true),
					numbers[1],
					numbers[2] == 0 ? "" : "/",
					numbers[2] == 0 ? (object) "" : numbers[2],
					wildcard
					);
			}
			_equations.Add(equation);
		}
		Debug.LogFormat( "Derivatives #{0} the equations are:\n{1}", moduleId, _equations.Join("\n"));
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
		var httpWebRequest = (HttpWebRequest) WebRequest.Create("http://api.mathjs.org/v4/");
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
			var httpResponse = (HttpWebResponse) httpWebRequest.GetResponse();
		
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
		Debug.LogFormat( "Derivatives #{0} the solutions are:\n{1}", moduleId, _solutions.Join("\n"));
	}

	void handleConnectionError(Exception error)
	{
		_error = true;
		SetEquationText(error.Message);
		SetScreenText("Press a button to solve module");
	}

	void checkSolve()
	{
		if (_error){ moduleSolved = true; return; }
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
					,ReplaceOneDividedByX)
					,ReplaceMoveMinus)
			)
			.ToList();
			
		
		//answerList = Regex.Split(answerList, @"(?<!(\(|\/|\*))(-|\+)(?!\()")
		var solutionList = Regex.Split(_solutions[_currentEquation].Replace("+ -", "- ")
				, @"(?<= )(?=-)(?=. )|\+")
			.Where(x => x != string.Empty)
			.ToList()
			.Select(x => r3.Replace(r2.Replace(r.Replace(
					x.Replace(" ", "").Replace("*", "")
					,ReplaceEquationBrackets)
					,ReplaceOneDividedByX)
					,ReplaceMoveMinus)
			)
			.ToList();
		
		answerList.Sort();
		solutionList.Sort();

		if (answerList.SequenceEqual(solutionList))
		{
			Debug.LogFormat("Derivatives #{0}: equation {1} solved correctly", moduleId, _currentEquation);

			if (_currentEquation+1 == _solvesNeeded)
			{
				GetComponent<KMAudio>().PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.CorrectChime, transform);
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
			Debug.LogFormat("Derivatives #{0}: equation {1} answer incorrect \n expected: {2}\n(raw): {3}\n but got: {4} \n(raw): {5}", moduleId, _currentEquation, solutionList.Join(),_solutions[_currentEquation],answerList.Join(), screen.text);
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
		GetComponent<KMAudio>().PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.Strike, transform);
		GetComponent<KMBombModule>().HandleStrike();
		if (_currentEquation + 1 == _solvesNeeded)
		{
			ModuleSolve();
		}else nextEquation();
	}
	void ModuleSolve()
	{
		moduleSolved = true;
		GetComponent<KMBombModule>().HandlePass();
	}
}
