using System.Collections.Generic;

namespace MissionControl.Domain.Entities
{
    public class Team
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public ICollection<Agent> Agents { get; set; } = new List<Agent>();
        public int ProjectId { get; set; }
        public Project Project { get; set; } = null!;
    }
}