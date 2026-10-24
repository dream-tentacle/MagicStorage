using Verse;

namespace RavenRace.Features.Operator.Gifting;

[StaticConstructorOnStartup]
public static class GiftingManager
{
	private static readonly GiftReactionDef defaultReaction;

	static GiftingManager()
	{
		defaultReaction = DefDatabase<GiftReactionDef>.GetNamed("Raven_GiftReaction_Default", true);
	}

	public static void HandleGift(Thing gift, int count, out int favorChange)
	{
		favorChange = 0;
		if (gift != null && count > 0)
		{
			GiftReactionDef giftReactionDef = defaultReaction;
			float num = gift.MarketValue * (float)count;
			favorChange = (int)(num * giftReactionDef.favorChangePerValue);
			WorldComponent_OperatorManager.ChangeFavorability(favorChange);
			string text = giftReactionDef.specialMessage;
			if (string.IsNullOrEmpty(text))
			{
				text = ((favorChange >= 500) ? "“……你这家伙，到底是抢了哪里的金库？好吧，你的心意我收下了。”" : ((favorChange >= 100) ? "“嗯，真是一份大礼，谢了。”" : ((favorChange >= 10) ? "“……收到了。”" : "“……就这？”")));
			}
			WorldComponent_OperatorManager.PostGiftMessage = text;
		}
	}
}
