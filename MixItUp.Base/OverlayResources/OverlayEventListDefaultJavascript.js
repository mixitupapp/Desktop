const list = document.getElementById("list");

const itemTemplate = document.querySelector("#item");

const maxItems = {TotalToShow};
const addToTop = {AddToTop};

function clear(data)
{
    while (list.childElementCount > 0)
    {
        list.removeChild(list.lastElementChild);
    }
}

function add(data)
{
    let item = createItem(data);
    if (list.children.length >= maxItems)
    {
        if (addToTop)
        {
            removeAndAddItem(list.lastElementChild, item);
        }
        else
        {
            removeAndAddItem(list.firstElementChild, item);
        }
    }
    else
    {
        addItem(item);
    }
}

function createItem(itemData)
{
    let item = itemTemplate.content.cloneNode(true);
    
    let header = item.firstElementChild.querySelector(".header");
    header.textContent = itemData.User.DisplayName;
    
    let text = item.firstElementChild.querySelector(".text");
    text.textContent = itemData.Details;
    
    return item.firstElementChild;
}

function addItem(item)
{
    if (addToTop)
    {
        list.insertBefore(item, list.firstElementChild);
    }
    else
    {
        list.appendChild(item);
    }
    
    performAnimation("{ItemAddedAnimationFramework}", "{ItemAddedAnimationName}", item).then((result) =>
    {
        
    });
}

function removeAndAddItem(oldItem, newItem)
{
    performAnimation("{ItemRemovedAnimationFramework}", "{ItemRemovedAnimationName}", oldItem).then((result) =>
    {
        list.removeChild(oldItem);
        addItem(newItem);
    });
}