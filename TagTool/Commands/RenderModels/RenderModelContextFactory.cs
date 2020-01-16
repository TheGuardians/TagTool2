﻿using TagTool.Cache;
using TagTool.Tags.Definitions;
using TagTool.Commands.Geometry;

namespace TagTool.Commands.RenderModels
{
    static class RenderModelContextFactory
    {
        public static CommandContext Create(CommandContext parent, GameCache cache, CachedTag tag, RenderModel renderModel)
        {
            var groupName = cache.StringTable.GetString(tag.Group.Name);

            var context = new CommandContext(parent,
                string.Format("{0:X8}.{1}", tag.Index, groupName));

            Populate(context, cache, tag, renderModel);

            return context;
        }

        public static void Populate(CommandContext context, GameCache cache, CachedTag tag, RenderModel renderModel)
        {
            /*
            context.AddCommand(new SpecifyShadersCommand(cacheContext, tag, renderModel));
            context.AddCommand(new GetResourceInfoCommand(cacheContext, tag, renderModel));
            context.AddCommand(new ResourceDataCommand(cacheContext, tag, renderModel));
            context.AddCommand(new DumpRenderGeometryCommand(cacheContext, renderModel.Geometry));
            context.AddCommand(new ReplaceRenderGeometryCommand(cacheContext, tag, renderModel));
            context.AddCommand(new ExtractModelCommand(cacheContext, renderModel));
            context.AddCommand(new ExtractBitmapsCommand(cacheContext, tag, renderModel));
            */
        }
    }
}
