using Sublingual.App.Models;
using Sublingual.Domain.SpeakingPractice;

namespace Sublingual.App.Services;

public interface ISpeakingPracticeAiTutorFactory
{
    IAiTutorService Create(SpeakingPracticeSettings settings);
}
