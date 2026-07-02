namespace Smarticipate.Core;

// Stored as int (matches the codebase convention). The last two ship in the follow-up PR.
public enum QuestionType
{
    YesNo = 0,
    SingleChoice = 1,
    MultipleChoice = 2,
    FreeText = 3,
    Scale = 4,
    Numeric = 5,
    WordCloud = 6,
    Ranking = 7
}
