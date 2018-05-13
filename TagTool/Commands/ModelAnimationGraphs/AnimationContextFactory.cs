﻿using TagTool.Cache;
using TagTool.Commands;
using TagTool.Tags.Definitions;

namespace TagTool.Commands.ModelAnimationGraphs
{
    static class AnimationContextFactory
    {
        public static CommandContext Create(CommandContext parent, HaloOnlineCacheContext info, CachedTagInstance tag, ModelAnimationGraph animation)
        {
            var groupName = info.GetString(tag.Group.Name);

            var context = new CommandContext(parent,
                string.Format("{0:X8}.{1}", tag.Index, groupName));

            Populate(context, info, tag, animation);

            return context;
        }

        public static void Populate(CommandContext context, HaloOnlineCacheContext info, CachedTagInstance tag, ModelAnimationGraph animation)
        {
            context.AddCommand(new GetResourceInfoCommand(info, tag, animation));
            context.AddCommand(new ResourceDataCommand(info, tag, animation));
        }
    }
}
