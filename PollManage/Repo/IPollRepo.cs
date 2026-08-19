using PollManage.Models;

namespace PollManage.Repo
{
    public interface IPollRepo
    {
        Task<Poll?> GetByCodeAsync(string code);
        Task<bool> CodeExistsAsync(string code);
        Task<Poll> AddAsync(Poll poll);
        Task<Poll?> UpdateAsync(Poll poll);
        Task<Poll?> MarkHasVotesAsync(string code);
    }
}
