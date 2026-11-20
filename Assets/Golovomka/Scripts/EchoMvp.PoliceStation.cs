using UnityEngine;

namespace Golovomka
{
    public partial class EchoMvp
    {
        Interaction StationNearby()
        {
            if (Vector2.Distance(position, station.phone.position) < 1.6f) return Interaction.AnswerPhone;
            if (Vector2.Distance(position, station.exit.position) < 1.5f) return Interaction.EnterLaboratory;
            return Interaction.None;
        }
    }
}
