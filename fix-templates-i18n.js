const fs = require('fs');
const file = 'd:/Client/insolvex/Insolvex.Web/src/pages/TemplateSettingsPage.tsx';
let c = fs.readFileSync(file, 'utf8');
const orig = c.length;

// Replace 'Editează' in SystemTemplateCard button
c = c.replace(
  /(<Pencil className="h-3\.5 w-3\.5 mr-1" \/>)\s*\n\s*Editează(\s*<\/Button>\s*\n\s*<\/div>\s*\n\s*\);\s*\n\s*\}\s*\n\s*\/\/ [─\-]{2} Custom template card)/,
  '$1\n        {t.templateSettings.edit}$2'
);

// Replace 'Editează' in CustomTemplateCard button (the one with margin-left variant)
c = c.replace(
  /(<Pencil className="h-3\.5 w-3\.5 mr-1" \/>)\s*\n\s*Editează(\s*<\/Button>)/,
  '$1\n          {t.templateSettings.edit}$2'
);

// AI recognition text
c = c.replace(
  'Recunoaștere AI activă — documentele similare încărcate de practicieni vor fi auto-clasificate ca',
  'AI recognition active — similar documents uploaded by practitioners will be auto-classified as'
);

// New template form placeholder (name input in NewTemplateForm)
c = c.replace(
  'placeholder="Denumire șablon *"\n        className="w-full rounded-md border border-input bg-background px-3 py-2 text-sm outline-none focus:ring-1 focus:ring-primary"',
  'placeholder={t.templateSettings.templateNamePlaceholder}\n        className="w-full rounded-md border border-input bg-background px-3 py-2 text-sm outline-none focus:ring-1 focus:ring-primary"'
);

// createAndOpen + cancel button
c = c.replace(
  'Crează și deschide editorul',
  '{t.templateSettings.createAndOpen}'
);
c = c.replace(
  'onClick={onCancel}>Anulează</Button>',
  'onClick={onCancel}>{t.common.cancel}</Button>'
);

// handleDelete confirm
c = c.replace(
  '"Ștergi acest șablon? Acțiunea este ireversibilă."',
  't.templateSettings.deleteConfirm'
);

// Page title + description
c = c.replace(
  '<h1 className="text-xl font-bold">Șabloane documente</h1>',
  '<h1 className="text-xl font-bold">{t.templateSettings.pageTitle}</h1>'
);
c = c.replace(
  /(<p className="text-sm text-muted-foreground mt-1">\s*)\n\s*Definește conținutul șabloanelor obligatorii și crează șabloane custom cu câmpuri dinamice\.\s*\n\s*(<\/p>)/,
  '$1{t.templateSettings.pageDesc}$2'
);

// System tab required description (multi-line)
c = c.replace(
  'Aceste șabloane sunt obligatorii conform procedurii de insolvență. Definește conținutul HTML\n            cu câmpuri dinamice din dosar — vor fi completate automat la generare.',
  '{t.templateSettings.requiredDesc}'
);

// No system templates
c = c.replace(
  'Nu există șabloane de sistem. Contactează administratorul pentru inițializare.',
  '{t.templateSettings.noSystemTemplates}'
);

// Custom tab description (multi-line)
c = c.replace(
  'Șabloane create de tine pentru orice scop — notificări, adrese, rapoarte interne.\n            Inserează câmpuri dinamice din dosar, debitor, creditori și alte persoane implicate.',
  '{t.templateSettings.customDesc}'
);

// No custom templates
c = c.replace(
  'Nu ai creat încă niciun șablon custom.',
  '{t.templateSettings.noCustomTemplates}'
);

// Create first template button
c = c.replace(
  'Crează primul șablon',
  '{t.templateSettings.createFirstTemplate}'
);

// Denumire template placeholder in TemplateEditorPanel (name input)
c = c.replace(
  'placeholder="Denumire template…"',
  'placeholder={t.templateSettings.templateNamePlaceholder}'
);

fs.writeFileSync(file, c, 'utf8');
console.log('Done. File size changed from', orig, 'to', c.length);
// Report any remaining Romanian
const remaining = c.match(/(Editea|Crează|Anuleaz|Obligat|obligat|încarcă|ncăr|ânc|aboraa|abloane|ntent|definit|activ)/g);
console.log('Remaining Romanian-like matches:', remaining ? remaining.length : 0);
