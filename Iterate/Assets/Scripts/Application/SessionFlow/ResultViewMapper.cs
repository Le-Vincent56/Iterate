using Iterate.Domain.Trace;

namespace Iterate.Application.SessionFlow
{
    /// <summary>
    /// Copies the result under review into a <see cref="ResultView"/>.
    /// </summary>
    internal static class ResultViewMapper
    {
        /// <summary>
        /// Maps the context's result.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <returns>The view, or null outside Result Review.</returns>
        internal static ResultView Map(SessionContext context)
        {
            CurrentResult result = context.Result;
            if (result == null || context.Stage != SessionStage.ResultReview)
                return null;

            ExecutionRecord record = result.Record;
            return new ResultView(
                record.Header.ExecutionIdentity,
                record.FinalState.FinalOutput.Value,
                record.ResultValidity == ResultValidityStatus.Valid,
                result.Evaluation.Reached,
                context.Process.Counters.ExecutionsRemaining
            );
        }
    }
}