namespace TriInspector
{
    public abstract class TriCustomDrawer : TriPropertyExtension
    {
        internal int Order { get; set; }

        public abstract TriElement CreateElementInternal(TriProperty property, TriElement next);
    }
}