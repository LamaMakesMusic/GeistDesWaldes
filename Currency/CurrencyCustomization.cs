using System;

namespace GeistDesWaldes.Currency
{
    [Serializable]
    public class CurrencyCustomization
    {
        public string PointsToStringMessage;
        public string NotEnoughPointsMessage;
        public string TransferedPointsMessage;

        public CurrencyCustomization()
        {
            PointsToStringMessage = Dictionaries.ReplyDictionary.X_HAS_GATHERED_Y_POINTS;
            NotEnoughPointsMessage = Dictionaries.ReplyDictionary.YOU_ARE_LACKING_POINTS_FOR_THIS_ACTION;
            TransferedPointsMessage = Dictionaries.ReplyDictionary.X_TRANSFERED_Y_POINTS_TO_Z;
        }


        public void SetToStringMessage(string message, ToStringType type)
        {
            switch (type)
            {
                case ToStringType.Points:
                    PointsToStringMessage = message;
                    break;
                case ToStringType.NotEnough:
                    NotEnoughPointsMessage = message;
                    break;
                case ToStringType.Transfer:
                    TransferedPointsMessage = message;
                    break;
                default:
                    break;
            }
        }
        public string GetToStringMessage(ToStringType type)
        {
            return type switch
            {
                ToStringType.Points => PointsToStringMessage,
                ToStringType.NotEnough => NotEnoughPointsMessage,
                ToStringType.Transfer => TransferedPointsMessage,
                _ => "",
            };
        }
        public void ResetToStringMessage(ToStringType type)
        {
            switch (type)
            {
                case ToStringType.Points:
                    PointsToStringMessage = Dictionaries.ReplyDictionary.X_HAS_GATHERED_Y_POINTS;
                    break;
                case ToStringType.NotEnough:
                    NotEnoughPointsMessage = Dictionaries.ReplyDictionary.YOU_ARE_LACKING_POINTS_FOR_THIS_ACTION;
                    break;
                case ToStringType.Transfer:
                    TransferedPointsMessage = Dictionaries.ReplyDictionary.X_TRANSFERED_Y_POINTS_TO_Z;
                    break;
                default:
                    break;
            }
        }

        public enum ToStringType
        {
            Points = 0,
            NotEnough = 1,
            Transfer = 2
        }
    }
}
