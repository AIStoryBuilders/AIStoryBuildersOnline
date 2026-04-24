using AIStoryBuilders.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AIStoryBuilders.Services
{
    public interface IGraphBuilder
    {
        StoryGraph Build(Story story);
    }
}
