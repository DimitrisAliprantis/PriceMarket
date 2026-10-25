/* Product list with a "load more" button, used for category pages and search results.
   The server sorts, filters and cuts the list into pages. The browser only asks
   for the next page and adds the new cards under the old ones. */

const PAGE_SIZE = 36;

/* State of the list on screen, or null when no list is shown. A new object is created
   every time the list restarts, so a slow answer from an old list can notice it is stale. */
let productListState = null;

// Builds the API address for one page of the current list
function buildProductsUrl(state, page) {
    const base = state.kind === 'search'
        ? `/api/products/search?q=${encodeURIComponent(state.value)}`
        : `/api/products?categoryId=${encodeURIComponent(state.value)}`;

    let url = `${base}&page=${page}&pageSize=${PAGE_SIZE}`;
    if (currentSortOrder !== 'default') url += `&sort=${currentSortOrder}`;
    if (selectedRetailerName) url += `&retailer=${encodeURIComponent(selectedRetailerName)}`;
    return url;
}

// Starts a list from page 1. kind is 'category' or 'search', value is the category id or the search text
function startProductList(kind, value) {
    productListState = { kind, value, page: 0, count: 0, hasMore: true, loading: false };

    document.getElementById('dynamicContainer').innerHTML = `
        <div class="product-grid" id="pGrid"></div>
        <p id="productsStatus" style="color:#71717a; font-size:13px;"></p>
        <div class="load-more-wrap">
            <button class="load-more-btn" id="loadMoreBtn" onclick="loadMoreProducts()">Επόμενη σελίδα</button>
        </div>
    `;
    loadMoreProducts();
}

// Same list again from page 1, used when the sort order or the supermarket filter changes
function restartProductList() {
    if (productListState) startProductList(productListState.kind, productListState.value);
}

// Fetches the next page and appends it. Also runs for page 1 when a list starts.
async function loadMoreProducts() {
    const state = productListState;
    if (!state || state.loading || !state.hasMore) return;

    state.loading = true;
    updateLoadMoreUi(state);

    try {
        const res = await fetch(buildProductsUrl(state, state.page + 1));
        if (!res.ok) throw new Error('Request failed');
        const products = await res.json();

        // The list was restarted while we waited, so this answer belongs to an old list
        if (state !== productListState) return;

        state.page++;
        state.count += products.length;

        // A full page means there may be more, a short page means this was the last one
        state.hasMore = products.length === PAGE_SIZE;
        appendProductCards(products);
    } catch {
        if (state === productListState) showToast('Σφάλμα φόρτωσης προϊόντων');
    } finally {
        if (state === productListState) {
            state.loading = false;
            updateLoadMoreUi(state);
        }
    }
}

// Keeps the button, the status line and the product counter in sync with the state
function updateLoadMoreUi(state) {
    const button = document.getElementById('loadMoreBtn');
    const status = document.getElementById('productsStatus');
    const countLabel = document.getElementById('itemsCount');
    if (!button || !status) return;

    const firstLoad = state.count === 0;

    if (state.loading && firstLoad) {
        status.textContent = 'Φόρτωση προϊόντων...';
    } else if (firstLoad && !state.hasMore) {
        status.textContent = `Δεν βρέθηκαν προϊόντα${selectedRetailerName ? ' στο επιλεγμένο κατάστημα' : ''}.`;
    } else if (firstLoad) {
        // The first page failed, the button below works as a retry
        status.textContent = 'Δεν ήταν δυνατή η φόρτωση. Δοκιμάστε ξανά.';
    } else {
        status.textContent = '';
    }

    // A "+" after the number means there are more products on the server
    countLabel.textContent = firstLoad ? '' : `${state.count}${state.hasMore ? '+' : ''} προϊόντα`;

    button.style.display = state.hasMore && !(state.loading && firstLoad) ? '' : 'none';
    button.disabled = state.loading;
    button.textContent = state.loading ? 'Φόρτωση...' : 'Επόμενη σελίδα';
}

// Creates one card per product and adds them to the end of the grid
function appendProductCards(products) {
    const grid = document.getElementById('pGrid');
    if (!grid) return;

    products.forEach(p => {
        const card = document.createElement('div');
        card.className = 'p-card';

        // With a supermarket selected the API also returns that supermarket's own price
        let priceText = '--€';
        let priceLabel = 'Ελάχιστη';
        if (selectedRetailerName && p.retailerPrice !== null && p.retailerPrice !== undefined) {
            priceText = `${Number(p.retailerPrice).toFixed(2)}€`;
            priceLabel = selectedRetailerName;
        } else if (p.minimumPrice !== null && p.minimumPrice !== undefined) {
            priceText = `${Number(p.minimumPrice).toFixed(2)}€`;
        }

        /* Fixed layout first, then the product text is filled in with textContent,
           so a product name can never inject HTML into the page. */
        card.innerHTML = `
            <div class="p-card-top"></div>
            <div class="p-title"></div>
            <div class="p-pricing-row">
                <span class="p-price-label"></span>
                <div class="p-main-price"></div>
            </div>
            <button class="p-add-btn">+ Προσθήκη</button>
        `;
        const title = card.querySelector('.p-title');
        title.textContent = p.name || 'Χωρίς όνομα';
        title.title = p.name || '';
        card.querySelector('.p-price-label').textContent = priceLabel;
        card.querySelector('.p-main-price').textContent = priceText;

        // Image, with a placeholder when there is no image or it fails to load
        const top = card.querySelector('.p-card-top');
        const showPlaceholder = () => { top.innerHTML = '<div class="img-placeholder">[ IMG ]</div>'; };
        const imgSrc = formatImageUrl(p.imageURL || p.imageUrl || null);
        if (imgSrc) {
            const img = document.createElement('img');
            img.className = 'p-img';
            img.alt = p.name || '';
            img.loading = 'lazy';
            img.onerror = showPlaceholder;
            img.src = imgSrc;
            top.appendChild(img);
        } else {
            showPlaceholder();
        }

        // The add button must not also open the product page
        card.querySelector('.p-add-btn').onclick = (e) => { e.stopPropagation(); addToList(p); };
        card.onclick = () => openProductDetails(p);
        grid.appendChild(card);
    });
}