using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using TileEstimator.Application.Abstractions;

namespace TileEstimator.Infrastructure.Pdf;

/// <summary>
/// Renders the SPEC 17 proposal with QuestPDF: logo, company and customer details, scope,
/// itemised lines, totals, terms and an acceptance section.
/// <para>
/// The acceptance section deliberately describes itself as an approval record and not as a
/// legally binding signature; the MVP has no signature provider behind it.
/// </para>
/// </summary>
public sealed class QuotePdfGenerator : IQuotePdfGenerator
{
    private static readonly string Accent = Colors.Blue.Darken2;

    public byte[] Generate(QuotePdfModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(40);
                page.DefaultTextStyle(t => t.FontSize(10).FontColor(Colors.Grey.Darken4));

                page.Header().Element(h => ComposeHeader(h, model));
                page.Content().Element(c => ComposeContent(c, model));
                page.Footer().Element(f => ComposeFooter(f, model));
            });
        }).GeneratePdf();
    }

    private static void ComposeHeader(IContainer container, QuotePdfModel model)
    {
        container.PaddingBottom(15).Column(column =>
        {
            column.Item().Row(row =>
            {
                row.RelativeItem().Column(left =>
                {
                    if (model.CompanyLogo is { Length: > 0 })
                    {
                        left.Item().Height(50).Image(model.CompanyLogo).FitHeight();
                        left.Item().PaddingTop(5);
                    }

                    left.Item().Text(model.CompanyName).FontSize(16).SemiBold().FontColor(Accent);

                    foreach (var line in new[]
                             {
                                 model.CompanyAddressLine,
                                 model.CompanyPhone,
                                 model.CompanyEmail,
                                 model.CompanyLicenseNumber is null ? null : "License " + model.CompanyLicenseNumber
                             }.Where(v => !string.IsNullOrWhiteSpace(v)))
                    {
                        left.Item().Text(line!).FontSize(9).FontColor(Colors.Grey.Darken1);
                    }
                });

                row.ConstantItem(180).Column(right =>
                {
                    right.Item().AlignRight().Text("QUOTE").FontSize(22).Bold().FontColor(Accent);
                    right.Item().AlignRight().Text(model.QuoteNumber).FontSize(12).SemiBold();
                    right.Item().PaddingTop(6).AlignRight()
                        .Text("Date: " + Date(model.QuoteDate)).FontSize(9);
                    right.Item().AlignRight()
                        .Text("Valid until: " + Date(model.ExpiresAt)).FontSize(9);
                });
            });

            column.Item().PaddingTop(10).LineHorizontal(1).LineColor(Accent);
        });
    }

    private static void ComposeContent(IContainer container, QuotePdfModel model)
    {
        container.PaddingVertical(10).Column(column =>
        {
            column.Spacing(14);

            column.Item().Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text("Prepared for").FontSize(9).SemiBold().FontColor(Colors.Grey.Darken1);
                    c.Item().Text(model.CustomerName).SemiBold();
                    foreach (var line in new[] { model.CustomerAddressLine, model.CustomerPhone, model.CustomerEmail }
                                 .Where(v => !string.IsNullOrWhiteSpace(v)))
                    {
                        c.Item().Text(line!).FontSize(9);
                    }
                });

                row.RelativeItem().Column(c =>
                {
                    c.Item().Text("Project").FontSize(9).SemiBold().FontColor(Colors.Grey.Darken1);
                    c.Item().Text(model.ProjectName ?? model.Title ?? "-").SemiBold();
                    if (!string.IsNullOrWhiteSpace(model.ProjectAddressLine))
                    {
                        c.Item().Text(model.ProjectAddressLine).FontSize(9);
                    }
                });
            });

            if (!string.IsNullOrWhiteSpace(model.ScopeOfWork))
            {
                column.Item().Column(c =>
                {
                    c.Item().Text("Scope of work").FontSize(11).SemiBold().FontColor(Accent);
                    c.Item().PaddingTop(3).Text(model.ScopeOfWork).FontSize(9.5f);
                });
            }

            column.Item().Element(c => ComposeLineTable(c, model));
            column.Item().Element(c => ComposeTotals(c, model));

            if (!string.IsNullOrWhiteSpace(model.TermsAndConditions))
            {
                column.Item().Column(c =>
                {
                    c.Item().Text("Terms and conditions").FontSize(11).SemiBold().FontColor(Accent);
                    c.Item().PaddingTop(3).Text(model.TermsAndConditions).FontSize(8.5f)
                        .FontColor(Colors.Grey.Darken2);
                });
            }

            column.Item().Element(ComposeAcceptance);
        });
    }

    private static void ComposeLineTable(IContainer container, QuotePdfModel model)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(1.2f); // Room
                columns.RelativeColumn(3f);   // Description
                columns.RelativeColumn(1f);   // Quantity
                columns.RelativeColumn(0.7f); // Unit
                columns.RelativeColumn(1.1f); // Unit price
                columns.RelativeColumn(1.2f); // Total
            });

            table.Header(header =>
            {
                foreach (var (text, alignRight) in new[]
                         {
                             ("Room", false), ("Description", false), ("Qty", true),
                             ("Unit", false), ("Unit price", true), ("Total", true)
                         })
                {
                    var cell = header.Cell().Background(Accent).Padding(5);
                    var textItem = alignRight ? cell.AlignRight() : cell;
                    textItem.Text(text).FontColor(Colors.White).SemiBold().FontSize(9);
                }
            });

            var shade = false;
            foreach (var line in model.Lines)
            {
                var background = shade ? Colors.Grey.Lighten4 : Colors.White;
                shade = !shade;

                table.Cell().Background(background).Padding(4).Text(line.RoomName ?? "-").FontSize(9);
                table.Cell().Background(background).Padding(4).Text(line.Description).FontSize(9);
                table.Cell().Background(background).Padding(4).AlignRight().Text(Number(line.Quantity)).FontSize(9);
                table.Cell().Background(background).Padding(4).Text(line.Unit).FontSize(9);
                table.Cell().Background(background).Padding(4).AlignRight().Text(Money(line.UnitPrice)).FontSize(9);
                table.Cell().Background(background).Padding(4).AlignRight().Text(Money(line.TotalPrice)).FontSize(9);
            }
        });
    }

    private static void ComposeTotals(IContainer container, QuotePdfModel model)
    {
        container.AlignRight().Width(260).Column(column =>
        {
            AddTotal(column, "Materials", model.MaterialTotal);
            AddTotal(column, "Labor", model.LaborTotal);

            if (model.OtherTotal != 0m)
            {
                AddTotal(column, "Other", model.OtherTotal);
            }

            column.Item().PaddingVertical(3).LineHorizontal(0.5f).LineColor(Colors.Grey.Medium);
            AddTotal(column, "Subtotal", model.Subtotal);

            if (model.DiscountAmount != 0m)
            {
                AddTotal(column, "Discount", -model.DiscountAmount);
            }

            if (model.TaxAmount != 0m)
            {
                AddTotal(column, "Tax", model.TaxAmount);
            }

            column.Item().PaddingVertical(4).LineHorizontal(1).LineColor(Accent);
            column.Item().Row(row =>
            {
                row.RelativeItem().Text("Total").FontSize(13).Bold();
                row.ConstantItem(120).AlignRight().Text(Money(model.GrandTotal))
                    .FontSize(13).Bold().FontColor(Accent);
            });
        });
    }

    private static void AddTotal(ColumnDescriptor column, string label, decimal amount) =>
        column.Item().Row(row =>
        {
            row.RelativeItem().Text(label).FontSize(9.5f);
            row.ConstantItem(120).AlignRight().Text(Money(amount)).FontSize(9.5f);
        });

    private static void ComposeAcceptance(IContainer container)
    {
        container.PaddingTop(10).Border(1).BorderColor(Colors.Grey.Medium).Padding(12).Column(column =>
        {
            column.Item().Text("Acceptance").FontSize(11).SemiBold().FontColor(Accent);
            column.Item().PaddingTop(3).Text(
                    "To accept this quote, use the secure link in the email we sent you, or sign and return this page. "
                    + "Recording your acceptance here confirms the scope and price above; it is an approval record, "
                    + "not an electronic signature.")
                .FontSize(8.5f).FontColor(Colors.Grey.Darken2);

            column.Item().PaddingTop(20).Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().LineHorizontal(0.7f).LineColor(Colors.Grey.Darken1);
                    c.Item().PaddingTop(2).Text("Customer name").FontSize(8);
                });
                row.ConstantItem(25);
                row.RelativeItem().Column(c =>
                {
                    c.Item().LineHorizontal(0.7f).LineColor(Colors.Grey.Darken1);
                    c.Item().PaddingTop(2).Text("Date").FontSize(8);
                });
            });
        });
    }

    private static void ComposeFooter(IContainer container, QuotePdfModel model)
    {
        container.Column(column =>
        {
            if (!string.IsNullOrWhiteSpace(model.FooterNote))
            {
                column.Item().PaddingBottom(4).Text(model.FooterNote)
                    .FontSize(8).FontColor(Colors.Grey.Darken1);
            }

            column.Item().Row(row =>
            {
                row.RelativeItem().Text(model.CompanyName).FontSize(8).FontColor(Colors.Grey.Medium);
                row.RelativeItem().AlignRight().Text(text =>
                {
                    text.DefaultTextStyle(s => s.FontSize(8).FontColor(Colors.Grey.Medium));
                    text.Span("Page ");
                    text.CurrentPageNumber();
                    text.Span(" of ");
                    text.TotalPages();
                });
            });
        });
    }

    private static string Money(decimal value) =>
        (value < 0m ? "-$" : "$") + Math.Abs(value).ToString("N2", CultureInfo.InvariantCulture);

    private static string Number(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    private static string Date(DateTime value) => value.ToString("MMM d, yyyy", CultureInfo.InvariantCulture);
}
