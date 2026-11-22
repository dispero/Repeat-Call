using UnityEngine;

namespace Golovomka
{
    public partial class EchoMvp
    {
        Interaction StationNearby()
        {
            if (Vector2.Distance(position, station.phone.position) < 1.6f) return Interaction.AnswerPhone;
            if (Vector2.Distance(position, station.exit.position) < 1.5f) return Interaction.EnterLaboratory;
            if (station.noticeBoard && Vector2.Distance(position, station.noticeBoard.position) < 1.2f)
                return Interaction.ReadNoticeBoard;
            return Interaction.None;
        }

        void ReadNoticeBoard()
        {
            if (zone != 0 || !station.noticeBoard) return;
            Dialog(string.Empty, string.Join("\n\n",
                station.leftNotice, station.rightNotice, station.lowerNotice));
        }
    }
}
