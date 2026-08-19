using VoteManage.Models;

namespace VoteManage.Repo
{
    public interface IVoteRepo
    {
        Task<bool> HasVotedAsync(string pollCode, string voterToken);
        Task<Vote?> AddAsync(Vote vote);
        Task<IEnumerable<Vote>> GetByPollCodeAsync(string pollCode);
    }
}
