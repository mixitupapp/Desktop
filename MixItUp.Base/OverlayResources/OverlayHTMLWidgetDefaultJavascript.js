function update(data)
{
    document.body.innerHTML = data.HTML;
}

sendParentMessage({ Type: "WidgetLoaded", ID: "{ID}" });