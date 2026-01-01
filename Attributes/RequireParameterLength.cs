using Discord.Commands;
using System;
using System.Threading.Tasks;

namespace GeistDesWaldes.Attributes
{
    public class RequireParameterLengthAttribute : ParameterPreconditionAttribute
    {
        private readonly int _minLength;
        private readonly int _maxLength;

        public RequireParameterLengthAttribute(int minLength, int maxLength)
        {
            _minLength = minLength;
            _maxLength = maxLength;
        }

        public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, ParameterInfo parameter, object value, IServiceProvider services)
        {
            return Task.Run(() =>
            {
                if (value is string str)
                {
                    if (str.Length < _minLength)
                        return PreconditionResult.FromError($"A parameter is too short. Min Length: {_minLength} characters.");

                    if (str.Length > _maxLength)
                        return PreconditionResult.FromError($"A parameter is too long. Max Length: {_maxLength} characters.");

                    return PreconditionResult.FromSuccess();
                }

                return PreconditionResult.FromError("Provided Parameter is not a string!");
            });

        }
    }
}
