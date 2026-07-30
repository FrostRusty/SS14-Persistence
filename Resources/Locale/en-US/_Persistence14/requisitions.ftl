requisitions-title = Requisitions Console

# Tabs
requisitions-tab-checkout = Checkout
requisitions-tab-config = Pricing & fees
requisitions-tab-config-locked = Pricing & fees (access)

# Header
requisitions-linked-count = {$count} machines linked
requisitions-flatpacker-online = flatpacker: online
requisitions-flatpacker-offline = flatpacker: not linked

# Catalogue / cart
requisitions-search-placeholder = Search catalogue…
requisitions-catalogue-header = Available to print
requisitions-catalogue-count = {$count} items
requisitions-cart-header = Cart
requisitions-cart-count = {$count ->
    [one] {$count} item
   *[other] {$count} items
}
requisitions-cart-empty = Cart is empty. Click items on the left to add them.
requisitions-add = + add
requisitions-prints-remaining = {$count} left
requisitions-flatpack = flatpack
requisitions-flatpack-note = +{$percent}% material, adds flatpack fee

# Stock
requisitions-stock-header = Department stock (live)
requisitions-stock-used = (−{$used})
requisitions-stock-none = No material silo linked — link one on the pricing tab to show stock.

# Checkout
requisitions-contribute-header = Contribute your own materials
requisitions-contribute-hint = cuts the bill
requisitions-breakdown-header = Total cost breakdown
requisitions-material-line = {$name} ({$qty})
requisitions-fee-line = {$name} ({$count})
requisitions-summary-material = Material cost
requisitions-summary-fees = Fees
requisitions-summary-total = Total
requisitions-checkout-button = Confirm & print — lock spesos in machine
requisitions-cancel-button = Cancel — return my cash & sheets
requisitions-processing = PROCESSING A CHECKOUT — please wait
requisitions-pending-line = Inserted: {$pending} spesos    Locked: {$locked} spesos
requisitions-cash-inserted = Inserted {$spesos} spesos.
requisitions-checkout-done = Order printed. {$spesos} spesos locked in the machine.
requisitions-checkout-partial = Order printed ({$spesos} spesos locked). Some items were skipped — not enough materials or money.
requisitions-checkout-failed = Nothing could be printed: not enough materials or money.
requisitions-material-sheets = {$amount} {$unit}
requisitions-checkout-nomoney = You don't have enough spesos to pay for this.

# Config tab
requisitions-config-header = Raw material & fee pricing
requisitions-config-hint = Prices here drive every checkout total.
requisitions-config-add = + Add fee
requisitions-config-materials = Raw materials (price per sheet)
requisitions-config-fees = Fees
requisitions-config-links = Linked machines
requisitions-config-withdraw = Withdraw {$spesos} spesos
requisitions-config-eject = Eject {$count} stored board(s)
requisitions-links-none = No linkable machines in range.
requisitions-link-linked = ✓ linked
requisitions-link-inrange = in range
requisitions-link-outofrange = out of range
requisitions-assign-button = Assign
requisitions-save = Save
requisitions-new-fee-name = New fee
requisitions-kind-material = RAW MATERIAL
requisitions-kind-fee = ABSTRACT FEE
requisitions-fee-flatpack = Flatpack Fee
requisitions-fee-flatpack-managed = auto — flatpacker linked
requisitions-applies-all = all items
requisitions-applies-flatpack = flatpacked items
requisitions-applies-count = {$count ->
    [one] {$count} item
   *[other] {$count} items
}
requisitions-applies-none = no items yet

# Add/edit dialog
requisitions-dialog-add-title = Add to price list
requisitions-dialog-kind-raw = Raw resource
requisitions-dialog-kind-fee = Abstract fee
requisitions-dialog-pick = Pick a raw resource
requisitions-dialog-fee-name = Fee name
requisitions-dialog-price = Price (spesos)
requisitions-dialog-assign-title = {$name} — applies to
requisitions-dialog-assign-all = Apply to every item on the checkout

# Access
requisitions-access-denied = Access denied.
