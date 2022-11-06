using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MolePatrolAPI.Models
{
    internal class ScoreItem
    {
        public ScoreItem(string name, GameMode mode, int score, bool isUser)
        {
            Name = name;
            Mode = mode;
            Score = score;
            IsUser = isUser;
        }

        public string Name { get; set; }

        public GameMode Mode { get; set; }

        public int Score { get; set; }

        public bool IsUser { get; set; }
    }
}
