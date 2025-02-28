using Godot;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;


public partial class Main : Node2D
{
	private AnimatedSprite2D pet;
	private StyleAndAdjustSettings settings;
	private IntPtr lastActiveWindow = IntPtr.Zero;
	private float speed = 100f;
	private Vector2 goal;
	private bool onMovement = false;
	private bool isResting = false;
	private bool followMouse = false;
	private Timer _restTimer;
	private Timer _sitTimer;

	public override void _Ready()
	{
		pet = GetNode<AnimatedSprite2D>("Pet");
		Vector2 canvaSize = GetViewportRect().Size;
		settings = new StyleAndAdjustSettings(
			DisplayServer.ScreenGetSize(DisplayServer.WindowGetCurrentScreen()).X / canvaSize.X,
			pet.SpriteFrames.GetFrameTexture("Idle", 0).GetSize());
		pet.Position = new Vector2(canvaSize.X / 2, canvaSize.Y - settings.GetAnimationAdjustValue("Idle", false));
		pet.Play("Idle");
		MakeWindowClickThrough(WindowsApiFunctions.GetActiveWindow());
		_restTimer = new Timer();
		_restTimer.WaitTime = 5.0;
		_restTimer.OneShot = true;
		_restTimer.Timeout += Rest;
		AddChild(_restTimer);
		_sitTimer = new Timer();
		_sitTimer.WaitTime = 5.0;
		_sitTimer.OneShot = true;
		_sitTimer.Timeout += SitDown;
		AddChild(_sitTimer);
	}
	public override void _Process(double delta)
	{
		DetectNewWindow();
		if (CheckIfNightTime())
		{
			if (onMovement)
				MoveToRestCorner(delta);
			else
				pet.Play("Sleep");
			return;
		}
		if (followMouse && GetWindow().HasFocus())
		{
			goal = GetGlobalMousePosition();
			onMovement = true;
			isResting = false;
			_restTimer.Stop();
			_sitTimer.Stop();
		}

		if (onMovement)
			if (isResting)
				MoveToRestCorner(delta);
			else
				MovePetToGoal(delta);
	}
	public override void _Input(InputEvent @event)
	{
		if (@event is InputEventKey keyEvent && keyEvent.Pressed && keyEvent.Keycode == Key.F)
		{
			followMouse = !followMouse;
		}
	}
	private void MakeWindowClickThrough(IntPtr hwnd)
	{
		int exStyle = settings.GetWS_EX_TRANSPARENT() | settings.GetWS_EX_LAYERED();
		WindowsApiFunctions.SetWindowLong(hwnd, settings.GetGWL_EX_STYLE(), exStyle);
	}
	private void DetectNewWindow()
	{
		IntPtr currentWindow = WindowsApiFunctions.GetForegroundWindow();
		if (currentWindow == IntPtr.Zero || currentWindow == lastActiveWindow)
			return;
		lastActiveWindow = currentWindow;
		if(WindowsApiFunctions.GetWindowRect(currentWindow, out RECT rect))
		{
			Vector2 canvaSize = GetViewportRect().Size;
			if (rect.Left / settings.aspectRelation < 0 || rect.Right / settings.aspectRelation > canvaSize.X)
				return;
			Vector2 bottomLeft = new Vector2((float)(rect.Left / settings.aspectRelation), (float)(rect.Bottom / settings.aspectRelation));
			Vector2 bottomRight = new Vector2((float)(rect.Right / settings.aspectRelation), (float)(rect.Bottom / settings.aspectRelation));
			Vector2 closestCorner = (pet.Position.DistanceTo(bottomLeft) < pet.Position.DistanceTo(bottomRight)) ?
				bottomLeft : bottomRight;
			if (Mathf.Abs(pet.Position.X - Mathf.Clamp(closestCorner.X, 0, canvaSize.X - settings.baseSpriteSize.X)) < 10)
				return;
			goal = new Vector2(Mathf.Clamp(closestCorner.X, 0, canvaSize.X - settings.baseSpriteSize.X), canvaSize.Y - settings.GetAnimationAdjustValue("Run", false));
			onMovement = true;
			_restTimer.Stop();
			_sitTimer.Stop();
			isResting = false;
		}
	}
	private void MovePetToGoal(double delta)
	{
		Vector2 canvaSize = GetViewportRect().Size;
		if (Mathf.Abs(goal.X - pet.Position.X) < 5)
		{
			pet.Position = new Vector2(pet.Position.X, canvaSize.Y - settings.GetAnimationAdjustValue("Trick", false));
			pet.Play("Trick");
			_restTimer.Start();
			onMovement = false;
		}
		else
		{
			pet.Position = new Vector2(pet.Position.X, canvaSize.Y - settings.GetAnimationAdjustValue("Run", false));
			pet.Play("Run");
			Vector2 course = new Vector2(goal.X - pet.Position.X, 0).Normalized();
			pet.Position += course * speed * (float)delta;
			pet.FlipH = course.X > 0;
		}
	}
	private void MoveToRestCorner(double delta)
	{
		Vector2 canvaSize = GetViewportRect().Size;
		goal = new Vector2(canvaSize.X - settings.baseSpriteSize.X / 2, canvaSize.Y);
		if (Mathf.Abs(goal.X - pet.Position.X) < 5)
		{
			pet.Position = new Vector2(pet.Position.X, canvaSize.Y - settings.GetAnimationAdjustValue("Idle", false));
			pet.FlipH = false;
			pet.Play("Idle");
			_sitTimer.Start();
			onMovement = false;
		}
		else
		{
			pet.Position = new Vector2(pet.Position.X, canvaSize.Y - settings.GetAnimationAdjustValue("Walk", false));
			pet.Play("Walk");
			Vector2 course = new Vector2(goal.X - pet.Position.X, 0).Normalized();
			pet.Position += course * speed * (float)delta;
			pet.FlipH = course.X > 0;
		}
	}
	private void Rest()
	{
		isResting = true;
		onMovement = true;
	}
	private void SitDown()
	{
		if (isResting)
		{
			pet.Play("Sit");
			_restTimer.Stop();
		}
	}
	private bool CheckIfNightTime()
	{
		int hour = DateTime.Now.Hour;
		return hour >= 0 && hour < 6;
	}
}

public static class WindowsApiFunctions
{
	[DllImport("user32.dll")]
	public static extern int SetWindowLong(IntPtr hwnd, int nIndex, int dwNewLong);
	[DllImport("user32.dll")]
	public static extern IntPtr GetActiveWindow();
	[DllImport("user32.dll")]
	public static extern IntPtr GetForegroundWindow();
	[DllImport("user32.dll")]
	public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
}

public struct RECT
{
	public int Left, Top, Right, Bottom;
}

public class StyleAndAdjustSettings
{
	// 64 x 49
	private const int GWL_EXSTYLE = -20;
	private const int WS_EX_TRANSPARENT = 0x00000020;
	private const int WS_EX_LAYERED = 0x00080000;
	private const int WS_EX_TOOLWINDOW = 0x00000080;
	private int WindowsTaskBar = 46;
	public double aspectRelation;
	public Vector2 baseSpriteSize;
	private readonly Dictionary<String, float> Idle = new() { { "X", 5 }, { "Y", 7 } };
	private readonly Dictionary<String, float> Run = new() { { "X", 1 }, { "Y", 4 } };
	private readonly Dictionary<String, float> Walk = new() { { "X", 1 }, { "Y", 3 } };
	private readonly Dictionary<String, float> Trick = new() { { "X", 1 }, { "Y", -1 } };
	private readonly Dictionary<String, float> Sit = new() { { "X", 1 }, { "Y", 1 } };

	public StyleAndAdjustSettings(double aspectRelation, Vector2 baseSpriteSize)
	{
		this.aspectRelation = aspectRelation;
		this.baseSpriteSize = baseSpriteSize;
	}

	public int GetGWL_EX_STYLE()
	{
		return GWL_EXSTYLE;
	}
	public int GetWS_EX_TRANSPARENT()
	{
		return WS_EX_TRANSPARENT;
	}
	public int GetWS_EX_LAYERED()
	{
		return WS_EX_LAYERED;
	}
	public int GetWS_EX_TOOLWINDOW()
	{
		return WS_EX_TOOLWINDOW;
	}
	public float GetAnimationAdjustValue(String animation, bool axis)
	{
		return animation switch
		{
			"Idle" => axis ? Idle["X"] : Idle["Y"] + WindowsTaskBar,
			"Run" => axis ? Run["X"] : Run["Y"] + WindowsTaskBar,
			"Walk" => axis ? Walk["X"] : Walk["Y"] + WindowsTaskBar,
			"Trick" => axis ? Trick["X"] : Trick["Y"] + WindowsTaskBar,
			"Sit" => axis ? Sit["X"] : Trick["Y"] + WindowsTaskBar,
			_ => -1
		};
	}
}