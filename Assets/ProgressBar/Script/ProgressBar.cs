using UnityEngine;
using UnityEngine.UI;

[ExecuteInEditMode]
public class ProgressBar : MonoBehaviour
{
    [Header("Title Setting")]
    public string Title;
    public Color TitleColor = Color.white;
    public Font TitleFont;
    public int TitleFontSize = 14;
    public bool DisplayAsPercentage = true; 

    [Header("Bar Setting")]
    public Color BarColor = Color.green;   
    public Color BarBackGroundColor = Color.gray;
    public Sprite BarBackGroundSprite;
    
    [Range(0f, 100f)]
    public float Alert = 20f;
    public Color BarAlertColor = Color.red;

    private Image bar;
    private Image barBackground;
    private Text txtTitle;
    private float barValue;
    private float _displayOverrideValue = -1f;
    private bool _isNA = false;

    // Layer Storage
    private int _currentLayer = 0;
    private int _totalLayers = 0;

    public float BarValue
    {
        get { return barValue; }
        set
        {
            _isNA = false; 
            barValue = Mathf.Clamp(value, 0, 100);
            UpdateValue(barValue);
        }
    }

    public void SetToNA()
    {
        _isNA = true;
        barValue = 0;
        _currentLayer = 0;
        _totalLayers = 0;
        UpdateValue(0);
    }

    // New specific method for the Progress Bar
    public void SetProgressWithLayers(float percent, int current, int total)
    {
        _isNA = false;
        _currentLayer = current;
        _totalLayers = total;
        BarValue = percent; // Triggers UpdateValue
    }

    public void SetValueWithRawText(float fillPercent, float rawValue)
    {
        _isNA = false; 
        barValue = Mathf.Clamp(fillPercent, 0, 100);
        _displayOverrideValue = rawValue;
        UpdateValue(barValue);
    }

    private void Awake()
    {
        Transform barTransform = transform.Find("Bar");
        if (barTransform != null) bar = barTransform.GetComponent<Image>();
        Transform bgTransform = transform.Find("BarBackground");
        if (bgTransform != null) barBackground = bgTransform.GetComponent<Image>();
        Transform textTransform = transform.Find("Text");
        if (textTransform != null) txtTitle = textTransform.GetComponent<Text>();
    }

    private void Start()
    {
        ApplyInitialSettings();
        UpdateValue(barValue);
    }

    private void ApplyInitialSettings()
    {
        if (txtTitle != null)
        {
            txtTitle.text = Title;
            txtTitle.color = TitleColor;
            txtTitle.font = TitleFont;
            txtTitle.fontSize = TitleFontSize;
            txtTitle.verticalOverflow = VerticalWrapMode.Overflow;
        }
        if (bar != null) bar.color = BarColor;
        if (barBackground != null)
        {
            barBackground.color = BarBackGroundColor; 
            barBackground.sprite = BarBackGroundSprite;
        }
    }

    void UpdateValue(float val)
    {
        if (bar != null)
        {
            bar.fillAmount = val / 100f;
            if (_isNA) bar.color = BarBackGroundColor; 
            else if (val <= Alert) bar.color = BarAlertColor;
            else bar.color = BarColor;
        }

        if (txtTitle != null)
        {
            if (_isNA)
            {
                txtTitle.text = (Title == "Progress") ? $"{Title}: N/A\nLayers: N/A" : $"{Title}: N/A";
            }
            else if (DisplayAsPercentage)
            {
                // Format with newline for Layers
                txtTitle.text = $"{Title}: {val:F1}%\nLayers: {_currentLayer}/{_totalLayers}";
            }
            else
            {
                float textVal = (_displayOverrideValue >= 0) ? _displayOverrideValue : val;
                txtTitle.text = $"{Title}: {textVal:F1}C";
            }
        }
    }

    private void Update()
    {
        if (!Application.isPlaying)
        {           
            ApplyInitialSettings();
            UpdateValue(barValue);
        }
    }
}