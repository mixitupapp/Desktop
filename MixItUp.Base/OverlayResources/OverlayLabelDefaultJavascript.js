var labels = document.getElementById("labels");

var displaySettings = "{DisplaySetting}";
var displayRotationSeconds = {DisplayRotationSeconds};
var displayRotationIndex = 0;

var displayEntranceAnimationFramework = "{DisplayEntranceAnimationFramework}";
var displayEntranceAnimationName = "{DisplayEntranceAnimationName}";
var displayExitAnimationFramework = "{DisplayExitAnimationFramework}";
var displayExitAnimationName = "{DisplayExitAnimationName}";

var currentLabelDisplay = null;
var pendingLabelDisplay = null;
var isTransitioning = false;

var dateTimeDisplays = [];
var dateTimeInterval = null;
var dateTimeFormatters = {};

// Longest tokens must come first so that MMMM is matched before MMM, and so on.
// Anything wrapped in square brackets is passed through as literal text, which
// is how a format keeps wording such as [Eindhoven] out of token replacement.
var dateTimeTokens = /\[[^\]]*\]|YYYY|YY|MMMM|MMM|MM|M|dddd|ddd|DD|D|HH|H|hh|h|mm|m|ss|s|A|a/g;

// Splits a format into HTML tags, HTML entities and plain text. Only the plain
// text runs through token replacement, otherwise markup such as class="a" or an
// entity such as &nbsp; would have its letters swapped out for date values.
var dateTimeSegments = /(<[^>]*>)|(&[a-zA-Z#][a-zA-Z0-9]*;)|([^<&]+)|([<&])/g;

// The .labelDisplay element carries the positioning transform, translate(-50%, -50%)
// when the widget is positioned by percentage. animate.css sets its own transform while
// an animation runs, which would replace that centering for the duration and then snap
// back when the class is removed. Content therefore lives in an inner element that the
// animation is applied to instead, so the two transforms never sit on the same element.
// It is built here rather than in the HTML template so saved widgets need no migration.
function ensureLabelContent(element)
{
    let content = element.querySelector(".labelContent");
    if (content == null)
    {
        element.innerHTML = "";
        content = document.createElement("div");
        content.className = "labelContent";
        element.appendChild(content);
    }
    return content;
}

// Swaps which display is on screen, playing the exit animation on the outgoing one
// before the entrance animation on the incoming one. When no animation is configured
// performAnimation resolves immediately, so this behaves exactly like an instant swap.
async function showLabelDisplay(next)
{
    if (next == null)
    {
        return;
    }

    // A request that arrives mid-transition is held until the current one finishes, so
    // rapid updates collapse to the most recent rather than animating over each other.
    if (isTransitioning)
    {
        pendingLabelDisplay = next;
        return;
    }

    isTransitioning = true;
    try
    {
        let target = next;
        while (target != null)
        {
            pendingLabelDisplay = null;

            // Skip the whole transition when the target is already showing, otherwise a
            // label with a single display would replay its entrance on every rotation.
            if (target !== currentLabelDisplay)
            {
                if (currentLabelDisplay != null)
                {
                    await performAnimation(displayExitAnimationFramework, displayExitAnimationName, ensureLabelContent(currentLabelDisplay));
                    currentLabelDisplay.style.visibility = 'hidden';
                }

                for (const labelDisplay of labels.children)
                {
                    if (labelDisplay !== target) { labelDisplay.style.visibility = 'hidden'; }
                }

                target.style.visibility = 'visible';
                currentLabelDisplay = target;

                await performAnimation(displayEntranceAnimationFramework, displayEntranceAnimationName, ensureLabelContent(target));
            }

            target = pendingLabelDisplay;
        }
    }
    finally
    {
        isTransitioning = false;
    }
}

async function rotateLabelDisplay()
{
    if (displayRotationIndex >= labels.children.length)
    {
        displayRotationIndex = 0;
    }

    let label = labels.children[displayRotationIndex];

    displayRotationIndex++;

    await showLabelDisplay(label);

    // Scheduled after the transition so the rotation value stays the on screen dwell time
    setTimeout(() => { rotateLabelDisplay(); }, (displayRotationSeconds * 1000));
}

function buildDateTimeFormatter(timeZone, options)
{
    if (timeZone)
    {
        try
        {
            return new Intl.DateTimeFormat(undefined, Object.assign({ timeZone: timeZone }, options));
        }
        catch (e)
        {
            // An unknown time zone throws, so fall back to the local time zone
            // rather than letting the whole widget stop rendering.
        }
    }
    return new Intl.DateTimeFormat(undefined, options);
}

function getDateTimeFormatters(timeZone)
{
    let key = timeZone || "";
    if (!dateTimeFormatters[key])
    {
        dateTimeFormatters[key] = {
            numeric: buildDateTimeFormatter(timeZone, { year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit', second: '2-digit', hour12: false }),
            long: buildDateTimeFormatter(timeZone, { weekday: 'long', month: 'long' }),
            short: buildDateTimeFormatter(timeZone, { weekday: 'short', month: 'short' })
        };
    }
    return dateTimeFormatters[key];
}

function getDateTimeValues(timeZone)
{
    const now = new Date();
    const formatters = getDateTimeFormatters(timeZone);

    let values = {};
    for (const part of formatters.numeric.formatToParts(now)) { values[part.type] = part.value; }
    for (const part of formatters.long.formatToParts(now))
    {
        if (part.type === 'weekday') { values.weekdayLong = part.value; }
        if (part.type === 'month') { values.monthLong = part.value; }
    }
    for (const part of formatters.short.formatToParts(now))
    {
        if (part.type === 'weekday') { values.weekdayShort = part.value; }
        if (part.type === 'month') { values.monthShort = part.value; }
    }

    // Some browsers report midnight as hour 24 when hour12 is false.
    values.hour24 = parseInt(values.hour, 10) % 24;
    values.hour12 = (values.hour24 % 12) === 0 ? 12 : (values.hour24 % 12);

    return values;
}

function padDateTimeValue(value, length)
{
    return String(value).padStart(length, '0');
}

function replaceDateTimeToken(token, values)
{
    if (token.charAt(0) === '[')
    {
        return token.substring(1, token.length - 1);
    }

    switch (token)
    {
        case 'YYYY': return padDateTimeValue(parseInt(values.year, 10), 4);
        case 'YY': return padDateTimeValue(parseInt(values.year, 10) % 100, 2);
        case 'MMMM': return values.monthLong;
        case 'MMM': return values.monthShort;
        case 'MM': return padDateTimeValue(parseInt(values.month, 10), 2);
        case 'M': return String(parseInt(values.month, 10));
        case 'dddd': return values.weekdayLong;
        case 'ddd': return values.weekdayShort;
        case 'DD': return padDateTimeValue(parseInt(values.day, 10), 2);
        case 'D': return String(parseInt(values.day, 10));
        case 'HH': return padDateTimeValue(values.hour24, 2);
        case 'H': return String(values.hour24);
        case 'hh': return padDateTimeValue(values.hour12, 2);
        case 'h': return String(values.hour12);
        case 'mm': return padDateTimeValue(parseInt(values.minute, 10), 2);
        case 'm': return String(parseInt(values.minute, 10));
        case 'ss': return padDateTimeValue(parseInt(values.second, 10), 2);
        case 's': return String(parseInt(values.second, 10));
        case 'A': return values.hour24 < 12 ? 'AM' : 'PM';
        case 'a': return values.hour24 < 12 ? 'am' : 'pm';
    }
    return token;
}

function formatDateTime(format, timeZone)
{
    const values = getDateTimeValues(timeZone);
    return format.replace(dateTimeSegments, function (match, tag, entity, text) {
        if (text)
        {
            return text.replace(dateTimeTokens, function (token) { return replaceDateTimeToken(token, values); });
        }
        return match;
    });
}

function refreshDateTimeDisplays()
{
    for (const display of dateTimeDisplays)
    {
        display.element.innerHTML = formatDateTime(display.format, display.timeZone);
    }
}

function registerDateTimeDisplay(element, data)
{
    let display = dateTimeDisplays.find(d => d.element === element);
    if (display != null)
    {
        display.format = data.DateTimeFormat;
        display.timeZone = data.TimeZone;
    }
    else
    {
        dateTimeDisplays.push({ element: element, format: data.DateTimeFormat, timeZone: data.TimeZone });
    }

    refreshDateTimeDisplays();

    if (dateTimeInterval == null)
    {
        dateTimeInterval = setInterval(refreshDateTimeDisplays, 1000);
    }
}

function add(data)
{
    let typeElement = document.getElementById(data.Type);
    if (typeElement == null) {
        let labelDisplayTemplate = document.getElementById("labeldisplay");
        const labelDisplay = labelDisplayTemplate.content.cloneNode(true);
        const labelText = labelDisplay.querySelector(".text");
        labelText.id = data.Type;
        labelText.style.visibility = 'hidden';
        const labelContent = ensureLabelContent(labelText);
        labelContent.innerHTML = data.Format;
        labels.appendChild(labelDisplay);

        if (data.DateTimeFormat)
        {
            registerDateTimeDisplay(labelContent, data);
        }

        if (labels.children.length == 1)
        {
            if (displaySettings == "RotatingDisplays")
            {
                rotateLabelDisplay();
            }
            else
            {
                showLabelDisplay(labels.children[0]);
            }
        }
    }
}

function update(data)
{
    let typeElement = document.getElementById(data.Type);
    if (typeElement != null) {
        const labelContent = ensureLabelContent(typeElement);
        if (data.DateTimeFormat)
        {
            registerDateTimeDisplay(labelContent, data);
        }
        else
        {
            labelContent.innerHTML = data.Format;
        }

        if (displaySettings == "NewestOnly")
        {
            showLabelDisplay(typeElement);
        }
    }
}

sendParentMessage({ Type: "WidgetLoaded", ID: "{ID}" });