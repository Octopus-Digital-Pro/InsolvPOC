namespace Insolvex.Domain.Enums;

/// <summary>
/// Recognised key document types that map to Romanian insolvency templates.
/// Each value corresponds to a .doc template in Templates-Ro/.
/// </summary>
public enum DocumentTemplateType
{
    /// <summary>Sentinta / hotarare de deschidere procedura (template 0)</summary>
    CourtOpeningDecision,

    /// <summary>Notificare creditori deschidere procedura + publicare BPI (template 1)</summary>
    CreditorNotificationBpi,

    /// <summary>Raport 40 zile / Art. 97 (template 2)</summary>
    ReportArt97,

    /// <summary>Tabel preliminar de creante (template 3)</summary>
    PreliminaryClaimsTable,

    /// <summary>Proces verbal AGC confirmare lichidator (template 4)</summary>
    CreditorsMeetingMinutes,

    /// <summary>Tabel definitiv de creante (template 5)</summary>
    DefinitiveClaimsTable,

    /// <summary>Raport final Art. 167 (template 7)</summary>
    FinalReportArt167,

    /// <summary>
    /// Notificare deschidere procedura — rendered from an HTML template to PDF.
    /// Template file: notificare_deschidere_procedura_template.html
    /// </summary>
    CreditorNotificationHtml,
}
