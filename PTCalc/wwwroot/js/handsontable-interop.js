let hotInstances = {};

// ---------------------------------------------------------------------------
// Sayı biçimi ve girdi normalleştirme
//
// GÖRÜNTÜ dile bağlıdır, GİRDİ ayrıştırması değildir. Girdi ayrıştırması dile
// bağlansaydı aynı dosyayı yapıştıran iki kullanıcı arayüz diline göre farklı
// sayılar elde ederdi. Bu yüzden aşağıdaki normalleştirici kültüre hiç bakmaz
// ve .NET tarafındaki NumericCellParser ile AYNI kuralı uygular:
//   * iki ayırıcı varsa sonda kalan ondalıktır  ("1.234,5" / "1,234.5" -> 1234.5)
//   * tek ayırıcı bir kez geçiyorsa ondalıktır  ("3,5" -> 3.5, "12,345" -> 12.345)
//   * aynı ayırıcı birden fazlaysa gruplamadır  ("1.234.567" -> 1234567)
// ---------------------------------------------------------------------------
(function registerNumbroLanguages() {
    if (typeof numbro === 'undefined' || typeof numbro.registerLanguage !== 'function') return;
    try {
        if (Object.keys(numbro.languages()).indexOf('tr-TR') === -1) {
            numbro.registerLanguage({
                languageTag: 'tr-TR',
                delimiters: { thousands: '.', decimal: ',' },
                abbreviations: { thousand: 'b', million: 'M', billion: 'Mr', trillion: 'T' },
                ordinal: function () { return '.'; },
                currency: { symbol: '₺', position: 'postfix', code: 'TRY' }
            });
        }
    } catch (e) {
        console.warn('numbro tr-TR dil paketi kaydedilemedi:', e);
    }
})();

function normalizeNumericInput(raw) {
    if (typeof raw !== 'string') return raw;
    const t = raw.trim();
    if (!t) return raw;

    const dots = (t.match(/\./g) || []).length;
    const commas = (t.match(/,/g) || []).length;
    let normalized;

    if (dots > 0 && commas > 0) {
        normalized = t.lastIndexOf('.') > t.lastIndexOf(',')
            ? t.replace(/,/g, '')
            : t.replace(/\./g, '').replace(',', '.');
    } else if (dots + commas === 1) {
        normalized = t.replace(',', '.');
    } else if (dots + commas > 1) {
        normalized = t.replace(/[.,]/g, '');
    } else {
        normalized = t;
    }

    const num = Number(normalized);
    // Sayıya benzemeyen değer olduğu gibi bırakılır; Handsontable geçersiz işaretler.
    return (normalized !== '' && isFinite(num)) ? num : raw;
}

// Handsontable, width:'100%' degerini KURULUM ANINDA piksele cevirir ve pencere
// yeniden boyutlandiginda kendiliginden yeniden olcmez. Bu yuzden grid, sayfa
// ilk acildigindaki genisligi kalici olarak sabitliyordu: daraltinca sayfa
// yatay kayiyor, genisletince de en genis hali yeni sabit deger oluyordu.
// Asagidaki dinleyici her yeniden boyutlamada kayitli tum ornekleri yeniden
// olcturur. destroy() ornegi kayittan sildigi icin ayrica temizlik gerekmiyor.
let hotResizeTimer = null;
window.addEventListener('resize', function () {
    clearTimeout(hotResizeTimer);
    hotResizeTimer = setTimeout(function () {
        Object.keys(hotInstances).forEach(function (id) {
            const instance = hotInstances[id];
            if (!instance || !instance.hot || instance.hot.isDestroyed) return;
            try {
                instance.hot.refreshDimensions();
            } catch (e) {
                console.warn('Handsontable yeniden olculemedi:', id, e);
            }
        });
    }, 150);
});

window.handsontableHelper = {
    initialize: function (elementId, dotNetHelper, options) {
        const container = document.getElementById(elementId);
        if (!container) {
            console.error('Handsontable container not found:', elementId);
            return;
        }

        console.log('=== Initializing Handsontable ===');
        console.log('Element ID:', elementId);
        console.log('Options received:', options);

        // Minimum satır sayısı
        const minRows = options.minRows || 20;

        // Kolon isimlerini al
        const columnNames = options.columns.map(col => col.data || col.Data);
        console.log('Column names:', columnNames);

        // Kolonları hazırla
        const columns = options.columns.map((col, index) => {
            const colConfig = {
                data: index, // Array index kullan
                type: col.type || col.Type || 'numeric',
                width: col.width || col.Width || 80,
                readOnly: col.readOnly || col.ReadOnly || options.readOnly || false,
                // culture .NET'ten geliyor (CultureInfo.CurrentCulture.Name); sabit
                // yazılmıyor ki ikinci dil eklenince burası değişmek zorunda kalmasın.
                numericFormat: {
                    pattern: col.numericFormat || col.NumericFormat || '0.00',
                    culture: options.culture || 'en-US'
                }
            };
            console.log(`Column ${index}:`, colConfig);
            return colConfig;
        });

        // Son seçilen satırı sakla
        let lastSelectedRow = 0;

        const hot = new Handsontable(container, {
            data: null,
            colHeaders: options.columns.map(c => c.title || c.Title),
            rowHeaders: true,
            width: '100%',
            height: options.height || 400,
            licenseKey: 'non-commercial-and-evaluation',
            columns: columns,
            // 'none': bildirilen sütun genişlikleri birebir uygulanır. 'all' idi;
            // o durumda genişlikler yalnızca oran gibi davranıp toplam kapsayıcıdan
            // küçük olan gridlerde şişiyordu (TTest'te 120 -> 542 px).
            stretchH: options.stretchH || 'none',
            manualColumnResize: true,
            autoWrapRow: true,
            minRows: minRows,
            minSpareRows: 0,
            // Girdi numbro'ya string olarak hiç ulaşmasın: burada gerçek sayıya
            // çevriliyor. Aksi halde sütun kültürü tr-TR iken "3.5" 35 olurdu.
            beforeChange: function (changes, source) {
                if (!changes) return;
                for (let i = 0; i < changes.length; i++) {
                    if (changes[i]) changes[i][3] = normalizeNumericInput(changes[i][3]);
                }
            },
            afterChange: function (changes, source) {
                if (source !== 'loadData' && changes) {
                    console.log('Cell changed:', changes, 'source:', source);
                    dotNetHelper.invokeMethodAsync('OnCellChanged', changes);
                }
            },
            afterSelection: function (row, col, row2, col2) {
                // Seçim değiştiğinde son satırı sakla
                lastSelectedRow = row;
                console.log('Selection changed, lastSelectedRow:', lastSelectedRow);
            }
        });

        console.log('Handsontable initialized successfully');
        
        hotInstances[elementId] = { 
            hot: hot, 
            columns: options.columns,
            columnNames: columnNames,
            getLastSelectedRow: function() { return lastSelectedRow; },
            setLastSelectedRow: function(row) { lastSelectedRow = row; }
        };
    },

    updateData: function (elementId, data) {
        const instance = hotInstances[elementId];
        if (!instance) {
            console.error('Handsontable instance not found:', elementId);
            return;
        }

        const hot = instance.hot;
        const columnNames = instance.columnNames;

        console.log('=== UpdateData ===');
        console.log('Element ID:', elementId);
        console.log('Data received:', data);
        console.log('Column names:', columnNames);

        if (!Array.isArray(data)) {
            console.error('Data is not an array:', typeof data);
            return;
        }

        // Veriyi array of arrays formatına dönüştür
        const tableData = data.map((item, rowIndex) => {
            // Eğer zaten array ise doğrudan kullan
            if (Array.isArray(item)) {
                return item;
            }
            
            // Object ise property'lerden değerleri al
            if (typeof item === 'object' && item !== null) {
                const row = columnNames.map(colName => {
                    let value = item[colName];
                    
                    // Eğer bulunamazsa, küçük harfle dene
                    if (value === undefined) {
                        const lowerProp = colName.charAt(0).toLowerCase() + colName.slice(1);
                        value = item[lowerProp];
                    }
                    
                    // Eğer hala bulunamazsa, büyük harfle dene
                    if (value === undefined) {
                        const upperProp = colName.charAt(0).toUpperCase() + colName.slice(1);
                        value = item[upperProp];
                    }
                    
                    return value !== undefined && value !== null ? value : null;
                });
                
                if (rowIndex === 0) {
                    console.log('First item properties:', Object.keys(item));
                    console.log('First row data:', row);
                }
                
                return row;
            }
            return [];
        });


        console.log('Table data prepared:', tableData.length, 'rows');
        console.log('First 2 rows:', tableData.slice(0, 2));
        
        hot.loadData(tableData);
        instance.setLastSelectedRow(0);
        console.log('Data loaded into Handsontable');
    },

    // Belirli hücreleri toplu güncelle - [[row, col, value], ...]
    setCells: function (elementId, changes) {
        const instance = hotInstances[elementId];
        if (!instance) {
            console.error('Handsontable instance not found:', elementId);
            return;
        }
        
        console.log('=== setCells ===');
        console.log('Changes count:', changes.length);
        
        const hot = instance.hot;
        hot.setDataAtCell(changes, 'setCells');
        console.log('Cells updated');
    },

    getData: function (elementId) {
        const instance = hotInstances[elementId];
        if (!instance) {
            console.error('Handsontable instance not found:', elementId);
            return null;
        }
        const data = instance.hot.getData();
        console.log('Getting data from Handsontable:', data.length, 'rows');
        return data;
    },

    destroy: function (elementId) {
        const instance = hotInstances[elementId];
        if (instance) {
            console.log('Destroying Handsontable:', elementId);
            instance.hot.destroy();
            delete hotInstances[elementId];
        }
    },

    addRow: function (elementId) {
        console.log('=== addRow called ===');
        
        const instance = hotInstances[elementId];
        if (!instance) {
            console.error('Handsontable instance not found for addRow:', elementId);
            return;
        }
        
        const hot = instance.hot;
        const selected = hot.getSelected();
        
        let insertAtRow;
        if (selected && selected.length > 0) {
            insertAtRow = selected[0][0];
        } else {
            // Seçim kaybolduysa son seçilen satırı kullan
            insertAtRow = instance.getLastSelectedRow();
        }
        
        console.log('Inserting row below row:', insertAtRow);
        
        hot.alter('insert_row_below', insertAtRow);
        
        // Yeni eklenen satıra geç
        const newRow = insertAtRow + 1;
        instance.setLastSelectedRow(newRow);
        hot.selectCell(newRow, 0);
        
        console.log('Row added. New row count:', hot.countRows());
    },

    deleteSelectedRow: function (elementId) {
        console.log('=== deleteSelectedRow called ===');
        
        const instance = hotInstances[elementId];
        if (!instance) {
            console.error('Handsontable instance not found for deleteSelectedRow:', elementId);
            return;
        }
        
        const hot = instance.hot;
        const selected = hot.getSelected();
        const rowCount = hot.countRows();
        
        if (rowCount <= 1) {
            console.log('Cannot delete: only one row remaining');
            return;
        }
        
        let rowToDelete;
        if (selected && selected.length > 0) {
            rowToDelete = selected[0][0];
        } else {
            // Seçim kaybolduysa son seçilen satırı kullan
            rowToDelete = instance.getLastSelectedRow();
        }
        
        console.log('Deleting row:', rowToDelete);
        console.log('Current row count:', rowCount);
        
        hot.alter('remove_row', rowToDelete);
        
        // Silinen satırdan sonraki satıra veya önceki satıra geç
        const newRowCount = hot.countRows();
        const newSelectedRow = Math.min(rowToDelete, newRowCount - 1);
        instance.setLastSelectedRow(newSelectedRow);
        hot.selectCell(newSelectedRow, 0);
        
        console.log('Row deleted. New row count:', newRowCount);
    }
};
