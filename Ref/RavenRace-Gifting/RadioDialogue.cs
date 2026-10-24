using System.Collections.Generic;
using System.Linq;
using RavenRace.Features.FusangOrganization.Communication;
using RavenRace.Features.FusangOrganization.Live2D;
using RavenRace.Features.Operator;
using RavenRace.Features.StoryEngine;
using UnityEngine;
using Verse;

namespace RavenRace.Features.FusangOrganization.UI;

public sealed class RadioDialogue
{
	private readonly StoryHandler story = new StoryHandler();

	private string fullText = string.Empty;

	private float cursor;

	public Texture2D Portrait { get; private set; }

	public bool BlocksNavigation
	{
		get
		{
			if (story.IsActive && story.CurrentNode != null)
			{
				return !story.CurrentNode.closeDialogue;
			}
			return false;
		}
	}

	public bool Speaking => cursor < (float)fullText.Length;

	public string FullText => fullText;

	public string VisibleText => fullText.Substring(0, Mathf.Min((int)cursor, fullText.Length));

	public RadioDialogue()
	{
		if (WorldComponent_OperatorManager.PostGiftMessage != null)
		{
			SetText(WorldComponent_OperatorManager.PostGiftMessage);
			WorldComponent_OperatorManager.PostGiftMessage = null;
		}
		else if (story.TryStartNewDialogue())
		{
			UpdateNode();
		}
		else
		{
			SetText("……（信号保持静默）");
		}
	}

	public void Subscribe()
	{
		ZuoYaoLive2DInteractionEffects.HeartEyesMilestoneReached += HeartEyes;
		ZuoYaoLive2DInteractionEffects.BreastTouchStarted += Touch;
	}

	public void Unsubscribe()
	{
		ZuoYaoLive2DInteractionEffects.HeartEyesMilestoneReached -= HeartEyes;
		ZuoYaoLive2DInteractionEffects.BreastTouchStarted -= Touch;
	}

	public void Update()
	{
		cursor = Mathf.Min((float)fullText.Length, cursor + Time.unscaledDeltaTime * 50f);
	}

	public void Skip()
	{
		cursor = fullText.Length;
	}

	public void SetText(string text)
	{
		if (!BlocksNavigation && !GenText.NullOrEmpty(text))
		{
			fullText = text.Replace("\\n", "\n");
			cursor = 0f;
		}
	}

	public void ShowGiftReceipt(string text)
	{
		story.EndStory();
		Portrait = null;
		ZuoYaoRadioLive2D.SetStoryExpression(null);
		SetText(text);
	}

	public void Wake()
	{
		SetText(ZuoYaoWakeDialogueUtility.GetRandomWakeLine());
	}

	private void Touch()
	{
		SetText(ZuoYaoBreastTouchDialogueUtility.GetRandomTouchLine());
	}

	private void HeartEyes(int count)
	{
		SetText("哦齁齁齁齁齁齁齁齁齁，忍不住了");
	}

	public void Choices(RadioContent content)
	{
		if (!BlocksNavigation)
		{
			return;
		}
		List<StoryOption> list = story.CurrentNode.options.Where((StoryOption c) => c.conditions.TrueForAll((StoryCondition condition) => condition.IsMet())).ToList();
		foreach (StoryOption item in list)
		{
			StoryOption choice = item;
			content.Button(choice.text, delegate
			{
				if (BlocksNavigation && story.CurrentNode.options.Contains(choice) && choice.conditions.TrueForAll((StoryCondition condition) => condition.IsMet()))
				{
					story.SelectOption(choice);
					if (choice.closeDialogue || GenText.NullOrEmpty(choice.nextNodeID))
					{
						story.EndStory();
					}
					if (story.IsActive)
					{
						UpdateNode();
					}
					else
					{
						Portrait = null;
						ZuoYaoRadioLive2D.SetStoryExpression(null);
					}
				}
			});
			if (!GenText.NullOrEmpty(choice.tooltip))
			{
				content.Text(choice.tooltip);
			}
		}
		if (list.Count == 0)
		{
			content.Text("当前没有满足条件的对话选项。可关闭通讯后继续游戏。");
		}
	}

	private void UpdateNode()
	{
		StoryNode currentNode = story.CurrentNode;
		fullText = (currentNode.text ?? string.Empty).Replace("\\n", "\n");
		cursor = 0f;
		ZuoYaoRadioLive2D.SetStoryExpression(currentNode.expressionKey);
		Portrait = (GenText.NullOrEmpty(currentNode.portraitPath) ? null : ContentFinder<Texture2D>.Get(currentNode.portraitPath, true));
	}
}
