using System.Text.RegularExpressions;
using Encargos.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using static Encargos.Win.K;

namespace Encargos.Win;

public static class NoteUtil
{
    public static readonly (string Key, Func<SolidColorBrush> Color)[] COLORS = { ("blue", () => Blue), ("green", () => Green), ("amber", () => Amber), ("rose", () => Rose), ("vio", () => Vio) };
    public static SolidColorBrush ColorOf(string k) => (COLORS.FirstOrDefault(c => c.Key == k).Color ?? (() => Blue))();
    static readonly Regex URL = new(@"\b((?:https?://|www\.)[^\s<>""']+|wa\.me/[^\s<>""']+)", RegexOptions.IgnoreCase);
    public static List<string> Links(string t) => URL.Matches(t).Select(m => m.Value.TrimEnd('.', ',', ';', ':', '!', '?', ')', ']')).Distinct().ToList();
    public static string Full(string u) => Regex.IsMatch(u, "^https?://", RegexOptions.IgnoreCase) ? u : "https://" + u;
}

public sealed class NotesPage : Screen
{
    public override string Title => "Notas";
    public override string Nav => "notes";
    public override string? Right { get { var n = Core.NotesSorted().Count; return $"{n} nota{(n == 1 ? "" : "s")}"; } }
    string _q = ""; StackPanel _list = new();

    public override void Build(StackPanel p)
    {
        _list = new StackPanel { Spacing = 12 };
        var all = Core.NotesSorted();
        var owe = all.Sum(n => Js.Num(n["amount"])); var oweN = all.Count(n => Js.Num(n["amount"]) > 0);
        if (owe > 0) p.Children.Add(Card(Row(V(1, T("Te deben en notas", 13.5, true, Mut), Muted($"{oweN} nota{(oweN == 1 ? "" : "s")} con monto")), Big(Money(owe), Rose, 22))));
        p.Children.Add(Btn("Nueva nota", () => S.Go(new NoteEditorPage(null)), true, null, ""));
        if (all.Count > 0) p.Children.Add(Search(_q, "Buscar en tus notas", q => { _q = q; Fill(); }));
        p.Children.Add(_list); Fill();
    }
    /// <summary>Nota en tarjeta de vidrio con punto de color (como la referencia).</summary>
    Border NoteCard(JObj n)
    {
        var col = NoteUtil.ColorOf(n.Str("color"));
        var tt = T(n.Str("title").IfEmpty("Sin título"), 15, true, null, true, 2); tt.FontWeight = Microsoft.UI.Text.FontWeights.ExtraBold;
        var items = new List<UIElement> { Lead(Dot(col), tt, Js.Truthy(n["pinned"]) ? new FontIcon { Glyph = "\uE718", FontSize = 12, Foreground = Mut } : null) };
        if (Js.Num(n["amount"]) > 0) items.Add(Chips(Chip("Me deben " + Money(n["amount"]), Rose)));
        if (n.Str("body").Length > 0) items.Add(T(n.Str("body"), 13.5, false, null, true, 6));
        foreach (var u in NoteUtil.Links(n.Str("body")))
        {
            var uu = u; var lb = Click(T("↗ " + Regex.Replace(u, "^https?://", ""), 12.5, true, Blue, false, 1), () => S.OpenUrl(NoteUtil.Full(uu))); lb.Padding = new Thickness(10, 8, 10, 8);
            items.Add(new Border { Child = lb, CornerRadius = new CornerRadius(10), Background = Tile, BorderBrush = LineBr, BorderThickness = new Thickness(1) });
        }
        var ph = (n["photos"] as JArr)?.OfType<string>().ToList() ?? new();
        if (ph.Count == 1) items.Add(Media.Photo(ph[0], double.NaN, 170, () => Media.Viewer(ph, 0, null), 700));
        else if (ph.Count > 1) items.Add(Media.Strip(ph, 84, i => Media.Viewer(ph, i, null)));
        items.Add(Muted(DayLabel(n["updated"]), 11));
        var id = n.Str("id");
        return ClickCard(() => S.Go(new NoteEditorPage(id)), items.ToArray());
    }
    void Fill()
    {
        _list.Children.Clear();
        var all = Core.NotesSorted(); var nq = Js.NormSearch(_q);
        var list = all.Where(n => nq.Length == 0 || Js.NormSearch(n.Str("title") + " " + n.Str("body")).Contains(nq)).ToList();
        if (all.Count == 0) { _list.Children.Add(Empty("Aún no tienes notas", "Guarda recordatorios, pedidos por hacer, direcciones o lo que necesites.")); return; }
        if (list.Count == 0) { _list.Children.Add(Empty("Ninguna nota coincide con la búsqueda.")); return; }
        var cols = S.ContentWidth < 760 ? 2 : 3;
        var pinned = list.Where(n => Js.Truthy(n["pinned"])).ToList(); var rest = list.Where(n => !Js.Truthy(n["pinned"])).ToList();
        foreach (var (h, ns) in new[] { ("Fijadas", pinned), ("Otras notas", rest) })
        {
            if (ns.Count == 0) continue;
            if (pinned.Count > 0) _list.Children.Add(DotHeader(h, h == "Fijadas" ? Blue : Mut));
            _list.Children.Add(Grid2(ns.Select(n => (UIElement)NoteCard(n)).ToList(), cols));
        }

    }
}

public sealed class NoteEditorPage : Screen
{
    readonly string? _id; bool _loaded;
    string _title = "", _body = "", _amt = "", _color = "blue"; bool _pinned; readonly List<string> _photos = new();
    StackPanel _links = new(), _photoRow = new();
    public NoteEditorPage(string? id) { _id = id; }
    public override string Title => _id == null ? "Nueva nota" : "Editar nota";
    public override string Nav => "notes";
    public override bool Live => false;

    public override void Build(StackPanel p)
    {
        var n = _id == null ? null : Core.Notes().FirstOrDefault(x => x.Str("id") == _id);
        if (!_loaded)
        {
            _loaded = true;
            if (n != null)
            {
                _title = n.Str("title"); _body = n.Str("body"); _amt = Js.Num(n["amount"]) > 0 ? Js.ToFixed(Js.Num(n["amount"]), 2) : "";
                _color = n.Str("color").IfEmpty("blue"); _pinned = Js.Truthy(n["pinned"]); if (n["photos"] is JArr a) _photos.AddRange(a.OfType<string>());
            }
        }
        _links = new StackPanel { Spacing = 2 }; _photoRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        p.Children.Add(Box("Título", _title, v => _title = v));
        p.Children.Add(Box("Nota (puedes pegar enlaces https://…)", _body, v => { _body = v; DrawLinks(); }, true));
        p.Children.Add(_links); DrawLinks();
        p.Children.Add(Box("Me deben (opcional, $)", _amt, v => _amt = v));
        p.Children.Add(IconCard("\uEB9F", "Fotos de artículos", null, new ScrollViewer { Content = _photoRow, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, HorizontalScrollMode = ScrollMode.Enabled },
            Btn("Agregar foto", async () => { var d = await Media.PickPhoto(900, 0.55); if (d != null) { _photos.Add(d); DrawPhotos(); S.Toast("📷 Foto agregada. Pulsa \"Guardar nota\" para guardarla."); } }, false, null, "")));
        DrawPhotos();
        var colors = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        foreach (var (k, c) in NoteUtil.COLORS)
        {
            var key = k;
            var b = new Button { Width = 34, Height = 34, CornerRadius = new CornerRadius(17), Background = c(), BorderBrush = Text, BorderThickness = new Thickness(k == _color ? 3 : 0) };
            b.Click += (_, _) => { _color = key; Redraw(); }; colors.Children.Add(b);
        }
        p.Children.Add(colors);
        p.Children.Add(Toggle("Fijar arriba", "Las notas fijadas aparecen primero", _pinned, v => _pinned = v));
        if (n != null) p.Children.Add(Muted("Creada: " + Js.Fmt(n["created"]) + (Js.Num(n["updated"]) != Js.Num(n["created"]) ? " · Editada: " + Js.Fmt(n["updated"]) : ""), 11));
        p.Children.Add(Cols(Btn("Guardar nota", Save, true, null, ""),
            n != null ? Btn("Eliminar", () => S.Ask("🗑️ Eliminar nota", "¿Eliminar esta nota? No se puede deshacer.", "Eliminar", true, () => { Core.DeleteNote(_id!); S.Back(); }), false, Rose)
                      : Btn("Cancelar", () => S.Back())));
    }
    void DrawLinks()
    {
        _links.Children.Clear(); var l = NoteUtil.Links(_body); if (l.Count == 0) return;
        _links.Children.Add(Muted("Enlaces en esta nota · pulsa para abrir"));
        foreach (var u in l) { var uu = u; _links.Children.Add(Link("↗ " + u, () => S.OpenUrl(NoteUtil.Full(uu)), Blue)); }
    }
    void DrawPhotos()
    {
        _photoRow.Children.Clear();
        for (var i = 0; i < _photos.Count; i++) { var idx = i; _photoRow.Children.Add(Media.Thumb(_photos[i], 100, i, _photos.Count, () => Media.Viewer(_photos.ToList(), idx, k => { _photos.RemoveAt(k); DrawPhotos(); }))); }
    }
    void Save()
    {
        var r = Core.SaveNote(_id, _title, _body, Math.Max(0, Num(_amt)), _color, _pinned, _photos.ToList());
        if (!r.Ok) { S.Toast(r.Error); return; }
        S.Toast(_id != null ? "✅ Nota actualizada." : "✅ Nota guardada."); S.Back();
    }
}
