# Katalog

An e-commerce catalogue where customers browse products and place orders against limited variant stock.

## Language

### Order lifecycle

**Order**:
A customer's request to buy a quantity of one variant, recorded once it has been accepted for processing.
_Avoid_: Purchase, transaction

**Refused**:
An order request turned down before any order exists, because stock looked insufficient or the variant did not exist when it was submitted.
_Avoid_: Rejected, declined

**Pending**:
An order that has been placed but whose stock has not yet been reserved.

**Confirmed**:
An order whose stock has been reserved.

**Rejected**:
A placed order that could not be confirmed for a business reason, such as stock running out before it was processed.
_Avoid_: Failed, refused

**Cancelled**:
A pending or confirmed order deliberately withdrawn by the customer who placed it or by an admin; any stock it had reserved is released.
_Avoid_: Deleted, rejected
