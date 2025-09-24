namespace RemSolution.Domain.Exceptions
{
    public class ForbiddenAccessException : Exception
    {
        public ForbiddenAccessException() : base("You do not have permission to perform this action.") { }
    }
}
