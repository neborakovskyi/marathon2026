import json
from pathlib import Path

out = Path(__file__).resolve().parent

def S(v):
    return {"S": v}

def N(v):
    return {"N": str(v)}

def BOOL(v):
    return {"BOOL": v}

def NULL():
    return {"NULL": True}

def L_S(values):
    return {"L": [S(v) for v in values]}

def put(item):
    return {"PutRequest": {"Item": item}}

def dump(name, table, items):
    path = out / name
    path.write_text(json.dumps({table: items}, indent=2) + "\n", encoding="utf-8")
    print(f"wrote {path.name} ({len(items)} items)")


# Existing IDs from dishes.json / locations.json
L1 = "7d598da3-0887-4a04-a3a1-fe26d8a4a4f2"
L2 = "2de08053-9bec-4662-bf56-2ccb70aeeb78"
L3 = "98c4acc4-ca8b-4416-be9d-ef40f1e73e0a"
L4 = "78b2074d-47ae-4b3c-82ae-75474298731e"
L5 = "ff74d3af-3a42-4581-94ff-06b7b6d33251"
L6 = "ec2142bb-2d14-489d-abe5-87c7a51488a3"
L7 = "591cc4cb-4843-418a-8024-3384195df46b"
L8 = "fd0705d3-cd4f-4dfe-8002-0c45da05a6e2"
L9 = "5efd6ff5-d614-4f55-a1cc-e723a8e2d5b4"
L10 = "f897c1a0-e81a-4d85-9c9d-e08901a196a3"
L11 = "fb8923ef-564a-4855-84db-1ebdd5d856d1"
L12 = "3e075f2a-5b65-4809-aca8-e1b331e80ea5"

# Waiter.UserId values (also used as waiterId / ProfileImages.userId)
W1 = "c470f7b9-b9d1-4f0f-b2bb-7ba34480567e"
W2 = "ab50bb00-6e53-480d-8157-d9c503a845dc"
W3 = "ac9cb6f6-3376-45a5-83a0-ae24ee013198"
W4 = "c0eb486b-f2cb-4211-8520-3e834032b6c6"
W5 = "ba4060db-5714-4524-b79b-25c6d7b5bc36"
W6 = "6e1b901f-d2d7-4522-8b66-7f81ec12bdda"
W7 = "f4eca63e-e264-4e46-970f-2821af2c0268"
W8 = "ac2f6083-d3ec-4d24-a009-a96f8910ae6e"
W9 = "4d8e2f1a-6b7c-4d9e-8f0a-1b2c3d4e5f60"
W10 = "5e9f3a2b-7c8d-4e0f-9a1b-2c3d4e5f6071"
W11 = "6f0a4b3c-8d9e-4f10-ab2c-3d4e5f607182"
W12 = "701b5c4d-9e0f-4011-bc3d-4e5f60718293"

C1 = "9a07da12-8191-4ba9-a626-ec8a0d0a8720"
C2 = "2b36210d-1741-4e57-8fdd-5d1e7c7b870c"
C3 = "ccc2b208-e324-43b7-8a5c-7fa544e3c2e9"
C4 = "4f17b8b2-ec03-41c7-b4f8-5d6315dc06dd"
C5 = "616d948f-8659-460e-8a93-599abe5ab85b"
C6 = "26e00714-d271-4989-97a5-8ae492a1bac9"
C7 = "668689fe-ce8d-4ddb-9bde-cc8db0f1ac97"
C8 = "a4f8a8ac-5a5c-4d65-ad40-3291d0770112"

T1 = "3c7dee65-9891-4753-a34c-03c8bfb6bc37"
T2 = "b68d0495-de73-4002-acb8-1a974f35b270"
T3 = "ed728c3e-2ea4-4855-a7a6-ea2562f01543"
T4 = "bb76039c-3f46-42ee-a436-7161e0c7abd5"
T5 = "f2dbfbf3-a4dd-49bb-9b76-5bdef925b7d9"
T6 = "9dbd74e4-d85c-43b3-9f39-5115202d5311"
T7 = "392eebc7-1438-4e13-8e60-6d60d446e515"
T8 = "49421929-447a-4fc8-9a58-d68f9b5cf9af"
T9 = "c5e02db1-28b7-4e85-a6bf-f53380b53fd5"
T10 = "f2114e48-e0ee-4dbf-b816-df17e213f37e"
T11 = "0df4f240-d20d-4d25-bc3c-32e39f834b04"
T12 = "a3f11ad5-2188-4aef-8b38-01f6a7559419"

R1 = "af1dd64c-1127-4f77-949d-62b37d0d7f05"
R2 = "6190941b-6700-4ce6-957e-fb57536e5c7c"
R3 = "89f7e15e-431f-45a4-9d49-ed71ecaa94fc"
R4 = "45baf726-e5a9-4044-9dc1-78f1ed46b764"
R5 = "eaefdea9-1c3a-4c1b-963b-adc9b7e521a4"
R6 = "e33ce69e-a50e-4375-a991-4c1de9541bca"
R7 = "720f669a-0b08-4349-8f62-68caf053371a"
R8 = "68b497c7-49be-485d-8762-35cf67a60f8b"
R9 = "d0da7100-db48-45a7-8bdc-b0cb018cd1d9"
R10 = "7aa9ad39-c5b1-4f24-88a3-5c72aad6ef7d"
R11 = "16986e39-8176-4d89-9dc3-b04dc6e7b73b"
R12 = "7e13b5a7-1980-4678-9714-d317cd1874fa"

D1 = "15673599-3a6f-44fc-8b76-e64f2f4f2ce3"
D2 = "95f0b087-4128-428f-bd3c-cc3353471e75"
D3 = "3a6058ab-493f-4dc7-b2df-3a492be1268d"
D4 = "1862cae4-8538-4905-92ca-7c849c701b3c"
D5 = "b370f005-6ae3-4eab-b38d-0ba54558e198"
D6 = "07efc9a8-e9fc-4612-9e80-59fda0dc7fc3"
D7 = "d2d165f7-4259-4a13-93cd-d20715e7d130"
D8 = "3c8207b9-945a-4a42-a4bc-5dad3eb3d992"
D9 = "79068272-ff05-4c11-8870-d49fcb43767c"
D10 = "4b83bf47-3c42-44f0-950f-f8af3a11db98"
D11 = "28df55d8-835b-4fd4-a21d-339d01207f3f"
D12 = "50e3096a-24e8-45a0-8089-5cd5802497cc"
D13 = "f2c1f663-de3a-4b20-973c-3896a63f1af7"
D14 = "3143f7ea-5a9a-4c5d-b4c2-9249f67af946"
D15 = "50f39742-80e6-457b-9ce0-8f34cd8278f4"

IMG = "https://restaurant-backend-dev-images-747482682250.s3.us-west-2.amazonaws.com/assets/img/locations/location_"

# DishType: Appetizer=0, MainCourse=1, Dessert=2
# DishState: OnStop=0, Available=1, OutOfStock=2
dishes = [
    (D1, "Grilled Salmon 2", "18.99", 1, 1, "320 g", "Fresh salmon fillet with lemon butter sauce and seasonal vegetables.", "540 kcal", "34 g", "28 g", "22 g", "Vitamin D, Vitamin B12, Omega-3", L1),
    (D2, "Truffle Mushroom Risotto", "22.5", 1, 1, "350g", "Creamy arborio rice cooked with wild mushrooms and truffle oil.", "620 kcal", "14g", "28g", "65g", "Vitamin D, Iron", L1),
    (D3, "Green Salad", "9.99", 1, 0, "220g", "Fresh vegetable salad", "180", "4g", "8g", "20g", "A, C, K", L1),
    (D4, "Margherita Pizza", "14.99", 1, 1, "450 g", "Classic pizza with tomato sauce, mozzarella, and basil.", "980 kcal", "36 g", "28 g", "120 g", "A, B12, C", L2),
    (D5, "Fresh Avocado Toast", "12", 1, 0, "200g", "Smashed avocado on sourdough with cherry tomatoes and chili flakes.", "320 kcal", "8g", "18g", "30g", "Vitamin C, Potassium", L2),
    (D6, "Grilled Salmon", "18.99", 1, 1, "320 g", "Fresh salmon fillet with lemon butter sauce and seasonal vegetables.", "540 kcal", "34 g", "28 g", "22 g", "Vitamin D, Vitamin B12, Omega-3", L2),
    (D7, "Margherita Pizza", "14.99", 1, 1, "450 g", "Classic pizza with tomato sauce, mozzarella, and basil.", "980 kcal", "36 g", "28 g", "120 g", "A, B12, C", L3),
    (D8, "Grilled Salmon", "18.99", 1, 1, "320 g", "Fresh salmon fillet with lemon butter sauce and seasonal vegetables.", "540 kcal", "34 g", "28 g", "22 g", "Vitamin D, Vitamin B12, Omega-3", L3),
    (D9, "Khachapuri Adjarian", "16.5", 1, 1, "380 g", "Boat-shaped bread filled with molten cheese, butter, and egg.", "890 kcal", "32 g", "48 g", "78 g", "Calcium, Vitamin A", L4),
    (D10, "Chocolate Lava Cake", "8.5", 1, 2, "160 g", "Warm chocolate cake with a molten center and vanilla ice cream.", "510 kcal", "7 g", "28 g", "58 g", "Iron", L5),
    (D11, "Lamb Shashlik", "21", 1, 1, "300 g", "Charcoal-grilled lamb skewers with onion and pomegranate molasses.", "640 kcal", "42 g", "36 g", "12 g", "Iron, Zinc, B12", L6),
    (D12, "Tomato Soup", "7.5", 1, 0, "250 g", "Roasted tomato soup with basil oil and croutons.", "210 kcal", "5 g", "8 g", "28 g", "Vitamin C, Lycopene", L7),
    (D13, "Tiramisu", "9", 1, 2, "180 g", "Classic mascarpone dessert with espresso-soaked ladyfingers.", "450 kcal", "8 g", "24 g", "48 g", "Calcium", L8),
    (D14, "Beef Steak", "27.5", 1, 1, "280 g", "Grilled ribeye with herb butter and roasted potatoes.", "780 kcal", "48 g", "52 g", "18 g", "Iron, B12, Zinc", L9),
    (D15, "Panna Cotta", "8", 0, 2, "150 g", "Vanilla panna cotta with berry coulis. Currently on stop.", "320 kcal", "4 g", "18 g", "32 g", "Calcium", L10),
]

dump("dishes.json", "Dishes", [
    put({
        "id": S(d[0]),
        "name": S(d[1]),
        "imageUrl": NULL(),
        "price": N(d[2]),
        "state": N(d[3]),
        "dishType": N(d[4]),
        "weight": S(d[5]),
        "description": S(d[6]),
        "calories": S(d[7]),
        "proteins": S(d[8]),
        "fats": S(d[9]),
        "carbohydrates": S(d[10]),
        "vitamins": S(d[11]),
        "locationId": S(d[12]),
    })
    for d in dishes
])

locations = [
    (L1, "9 Abashidze Street", "Set in an upscale, trendy district, this chic venue specializes in modern fusion cuisine, featuring a minimalist interior and a vibrant evening atmosphere.", 2, "90%", 4.6, [D1, D2]),
    (L2, "14 Baratashvili Street", "Nestled near the historic city walls, this cozy spot blends traditional brickwork with modern design, offering a warm dining experience just steps from the old town.", 6, "70%", 4.4, [D4, D6]),
    (L3, "48 Rustaveli Avenue", "Located on the city's bustling main cultural artery, this grand establishment features classic high ceilings and a wide outdoor terrace perfect for people-watching.", 2, "85%", 4.5, [D7]),
    (L4, "22 Chavchavadze Avenue", "A neighborhood bistro with an open kitchen and a focus on Ukraine classics.", 4, "75%", 4.3, [D9]),
    (L5, "5 Marjanishvili Street", "Industrial-chic dining room known for desserts and late-night service.", 8, "80%", 4.2, [D10]),
    (L6, "17 Aghmashenebeli Avenue", "Lively street-side terrace serving grilled meats and seasonal salads.", 4, "65%", 4.1, [D11]),
    (L7, "3 Freedom Square", "Compact lunch spot popular with office workers for soups and salads.", 2, "88%", 4.0, [D12]),
    (L8, "11 Pekini Avenue", "Quiet residential restaurant with a pastry counter and espresso bar.", 6, "60%", 4.4, [D13]),
    (L9, "8 Dadiani Street", "Steakhouse with a wine cellar and private dining rooms.", 4, "72%", 4.7, [D14]),
    (L10, "26 Kazbegi Avenue", "Modern cafe with a short tasting menu and weekend brunch.", 4, "55%", 3.9, [D15]),
    (L11, "4 Leselidze Street", "Old-town wine bar pairing small plates with natural wines.", 3, "68%", 4.3, []),
    (L12, "19 Tsereteli Avenue", "Family-friendly location with a large patio and kids' menu.", 10, "50%", 4.1, []),
]

dump("locations.json", "Locations", [
    put({
        "id": S(loc[0]),
        "address": S(loc[1]),
        "description": S(loc[2]),
        "totalCapacity": N(loc[3]),
        "averageOccupancy": S(loc[4]),
        "imageUrl": S(IMG + loc[0]),
        "rating": N(loc[5]),
        **({"specialtyDishIds": L_S(loc[6])} if loc[6] else {"specialtyDishIds": NULL()}),
    })
    for loc in locations
])

tables = [
    (T1, L1, 2, 1),
    (T2, L1, 4, 2),
    (T3, L2, 6, 1),
    (T4, L2, 4, 2),
    (T5, L3, 2, 1),
    (T6, L4, 4, 1),
    (T7, L5, 8, 1),
    (T8, L6, 4, 1),
    (T9, L7, 2, 1),
    (T10, L8, 6, 1),
    (T11, L9, 4, 1),
    (T12, L10, 4, 1),
]

dump("tables.json", "Tables", [
    put({"id": S(t[0]), "locationId": S(t[1]), "capacity": N(t[2]), "tableNumber": N(t[3])})
    for t in tables
])

# ReservationStatus: Reserved=0, Cancelled=1, InProgress=2, Completed=3
reservations = [
    (R1, T1, C1, W1, False, "2026-10-20T18:00:00.000Z", "2026-10-20T20:00:00.000Z", 2, 3),
    (R2, T2, C2, W1, True, "2026-10-21T12:00:00.000Z", "2026-10-21T13:30:00.000Z", 4, 2),
    (R3, T3, C3, W2, False, "2026-10-21T19:00:00.000Z", "2026-10-21T21:00:00.000Z", 5, 3),
    (R4, T4, C4, W2, False, "2026-10-22T18:30:00.000Z", "2026-10-22T20:30:00.000Z", 3, 0),
    (R5, T5, C5, W3, True, "2026-10-22T13:00:00.000Z", "2026-10-22T14:00:00.000Z", 2, 3),
    (R6, T6, C6, W4, False, "2026-10-23T19:00:00.000Z", "2026-10-23T21:30:00.000Z", 4, 0),
    (R7, T7, C7, W4, False, "2026-10-23T20:00:00.000Z", "2026-10-23T22:00:00.000Z", 6, 1),
    (R8, T8, C8, W5, True, "2026-10-24T18:00:00.000Z", "2026-10-24T20:00:00.000Z", 3, 0),
    (R9, T1, C1, W5, False, "2026-10-24T12:30:00.000Z", "2026-10-24T14:00:00.000Z", 2, 3),
    (R10, T3, C2, W2, False, "2026-10-25T19:00:00.000Z", "2026-10-25T21:00:00.000Z", 4, 2),
    (R11, T5, C3, W3, True, "2026-10-25T13:00:00.000Z", "2026-10-25T14:30:00.000Z", 2, 0),
    (R12, T2, C4, W1, False, "2026-10-26T18:00:00.000Z", "2026-10-26T20:00:00.000Z", 3, 0),
]

dump("reservations.json", "Reservations", [
    put({
        "id": S(r[0]),
        "tableId": S(r[1]),
        "customerId": S(r[2]),
        "waiterId": S(r[3]),
        "createdByWaiter": BOOL(r[4]),
        "startTime": S(r[5]),
        "endTime": S(r[6]),
        "guestCount": N(r[7]),
        "status": N(r[8]),
    })
    for r in reservations
])

# Waiters table (WaiterListItem) — included so Waiter.UserId matches reservation waiterId
dump("waiters.json", "Waiters", [
    put({"userId": S(W1), "locationId": S(L1), "createdAt": S("2026-09-01T08:00:00.000Z"), "reservations": L_S([R1, R2, R12])}),
    put({"userId": S(W2), "locationId": S(L2), "createdAt": S("2026-09-02T08:00:00.000Z"), "reservations": L_S([R3, R4, R10])}),
    put({"userId": S(W3), "locationId": S(L3), "createdAt": S("2026-09-03T08:00:00.000Z"), "reservations": L_S([R5, R11])}),
    put({"userId": S(W4), "locationId": S(L4), "createdAt": S("2026-09-04T08:00:00.000Z"), "reservations": L_S([R6, R7])}),
    put({"userId": S(W5), "locationId": S(L1), "createdAt": S("2026-09-05T08:00:00.000Z"), "reservations": L_S([R8, R9])}),
    put({"userId": S(W6), "locationId": S(L5), "createdAt": S("2026-09-06T08:00:00.000Z"), "reservations": {"L": []}}),
    put({"userId": S(W7), "locationId": S(L6), "createdAt": S("2026-09-07T08:00:00.000Z"), "reservations": {"L": []}}),
    put({"userId": S(W8), "locationId": S(L7), "createdAt": S("2026-09-08T08:00:00.000Z"), "reservations": {"L": []}}),
    put({"userId": S(W9), "locationId": S(L8), "createdAt": S("2026-09-09T08:00:00.000Z"), "reservations": {"L": []}}),
    put({"userId": S(W10), "locationId": S(L9), "createdAt": S("2026-09-10T08:00:00.000Z"), "reservations": {"L": []}}),
    put({"userId": S(W11), "locationId": S(L10), "createdAt": S("2026-09-11T08:00:00.000Z"), "reservations": {"L": []}}),
    put({"userId": S(W12), "locationId": S(L11), "createdAt": S("2026-09-12T08:00:00.000Z"), "reservations": {"L": []}}),
])

# FeedbackType: Service=0, CuisineExperience=1
# unique (reservationId, createdAt)
feedback = [
    (R1, L1, C1, "Excellent service, our waiter was attentive.", 5, "2026-10-20T20:05:00.000Z", 0),
    (R1, L1, C1, "Salmon was cooked perfectly.", 5, "2026-10-20T20:06:00.000Z", 1),
    (R2, L1, C2, "A bit slow at lunch, but friendly.", 4, "2026-10-21T13:35:00.000Z", 0),
    (R3, L2, C3, "Great table near the window.", 5, "2026-10-21T21:05:00.000Z", 0),
    (R3, L2, C3, "Pizza crust was outstanding.", 5, "2026-10-21T21:06:00.000Z", 1),
    (R5, L3, C5, "Quick lunch service.", 4, "2026-10-22T14:05:00.000Z", 0),
    (R5, L3, C5, "Salmon was a little dry.", 3, "2026-10-22T14:06:00.000Z", 1),
    (R6, L4, None, None, 5, "2026-10-23T21:35:00.000Z", 1),
    (R9, L1, C1, "Smooth brunch seating.", 4, "2026-10-24T14:05:00.000Z", 0),
    (R9, L1, C1, "Loved the risotto.", 5, "2026-10-24T14:06:00.000Z", 1),
    (R10, L2, C2, "Waiter checked in often.", 4, "2026-10-25T21:05:00.000Z", 0),
    (R4, L2, C4, "Looking forward to the meal.", 5, "2026-10-22T18:35:00.000Z", 0),
]

def feedback_item(f):
    reservation_id, location_id, user_id, comment, rating, created_at, feedback_type = f
    return put({
        "reservationId": S(reservation_id),
        "locationId": S(location_id),
        "userId": S(user_id) if user_id else NULL(),
        "comment": S(comment) if comment else NULL(),
        "rating": N(rating),
        "createdAt": S(created_at),
        "type": N(feedback_type),
    })

dump("feedback.json", "Feedback", [feedback_item(f) for f in feedback])

notifications = [
    ("8c395176-eab0-481d-991e-01fb8c681ee6", C1, "Your reservation is confirmed.", None, R1, False, "2026-10-19T10:00:00.000Z"),
    ("341fa159-ceb5-4363-8429-c2e7e58b8c02", C1, "Thanks for dining with us. Please leave feedback.", None, R1, True, "2026-10-20T20:10:00.000Z"),
    ("bb866400-9cf5-49c0-b3eb-be69880917b9", C2, "Your reservation starts in one hour.", None, R2, False, "2026-10-21T11:00:00.000Z"),
    ("14bf3e38-025b-40ea-bfe8-8fbd341f1993", W1, "New walk-in reservation assigned to you.", None, R2, False, "2026-10-21T11:55:00.000Z"),
    ("181187ed-fdb8-4b4c-bf15-ab1da36eda41", C3, "Your reservation is confirmed.", None, R3, True, "2026-10-20T09:00:00.000Z"),
    ("b05d023b-9290-4e95-81d2-dbad43422df8", C4, "Your reservation is booked.", None, R4, False, "2026-10-21T16:00:00.000Z"),
    ("94972b7c-a6e2-4758-a358-de72c38a45e2", C5, "Please rate your visit.", None, R5, True, "2026-10-22T14:10:00.000Z"),
    ("8039000f-172c-47f1-b187-24b1e86a5eba", C6, "Your table is ready.", None, R6, False, "2026-10-23T18:50:00.000Z"),
    ("10a617b0-f36b-41b4-a8c3-d427584b71ac", C7, "Reservation cancelled.", None, R7, True, "2026-10-23T12:00:00.000Z"),
    ("05d0dcd3-f6aa-48bf-84e0-badba097a132", W4, "A reservation at your location was cancelled.", None, R7, False, "2026-10-23T12:01:00.000Z"),
    ("5a88bd1e-7fa6-4695-a307-3ad88fba7f99", C8, "Waiter created a reservation for you.", None, R8, False, "2026-10-24T10:00:00.000Z"),
    ("21fe772b-30df-4785-8e5c-fd1424af2e63", C1, "Your brunch reservation is complete.", None, R9, True, "2026-10-24T14:10:00.000Z"),
]

dump("notifications.json", "Notifications", [
    put({
        "id": S(n[0]),
        "userId": S(n[1]),
        "message": S(n[2]),
        "orderId": NULL() if n[3] is None else S(n[3]),
        "reservationId": S(n[4]),
        "isRead": BOOL(n[5]),
        "createdAt": S(n[6]),
    })
    for n in notifications
])

profile_users = [W1, W2, W3, W4, W5, W6, W7, W8, W9, W10, W11, W12]
dump("profileImages.json", "ProfileImages", [
    put({"userId": S(uid), "imageKey": S(f"profile/{uid}.jpg")})
    for uid in profile_users
])

# Reports PK waiterId + date
report_rows = [
    (W1, L1, 4.7, 4, 4.8, 4, "1240.50", 8.0, "2026-10-20T00:00:00.000Z", 12),
    (W1, L1, 4.5, 4, 4.6, 3, "980.00", 7.5, "2026-10-21T00:00:00.000Z", 9),
    (W1, L1, 4.6, 4, 4.7, 4, "1105.25", 8.0, "2026-10-26T00:00:00.000Z", 10),
    (W2, L2, 4.8, 4, 4.9, 4, "1560.00", 8.5, "2026-10-21T00:00:00.000Z", 14),
    (W2, L2, 4.4, 3, 4.5, 3, "1320.75", 8.0, "2026-10-22T00:00:00.000Z", 11),
    (W2, L2, 4.6, 4, 4.7, 4, "1488.00", 8.0, "2026-10-25T00:00:00.000Z", 13),
    (W3, L3, 4.3, 3, 4.1, 3, "640.00", 6.0, "2026-10-22T00:00:00.000Z", 6),
    (W3, L3, 4.5, 4, 4.4, 3, "720.50", 6.5, "2026-10-25T00:00:00.000Z", 7),
    (W4, L4, 4.9, 5, 4.8, 4, "1880.00", 9.0, "2026-10-23T00:00:00.000Z", 16),
    (W6, L5, 4.2, 3, 4.0, 3, "910.00", 7.0, "2026-10-23T00:00:00.000Z", 8),
    (W5, L1, 4.6, 4, 4.7, 4, "870.00", 7.5, "2026-10-24T00:00:00.000Z", 8),
    (W7, L6, 4.4, 4, 4.5, 4, "760.25", 6.0, "2026-10-24T00:00:00.000Z", 7),
]
dump("reports.json", "Reports", [
    put({
        "waiterId": S(r[0]),
        "locationId": S(r[1]),
        "averageServiceRating": N(r[2]),
        "minimumServiceRating": N(r[3]),
        "averageCuisineRating": N(r[4]),
        "minimumCuisineRating": N(r[5]),
        "totalRevenue": N(r[6]),
        "hoursWorked": N(r[7]),
        "date": S(r[8]),
        "ordersProcessed": N(r[9]),
    })
    for r in report_rows
])

# ReportType: Service=0, Sales=1
history = [
    put({
        "id": S("e5d76fa8-89f7-44c4-863e-0bda3cd4abc6"),
        "reportType": N(0),
        "startDate": S("2026-10-13"),
        "endDate": S("2026-10-19"),
        "userId": S(W1),
        "locationId": S(L1),
        "locationName": S("9 Abashidze Street"),
        "waiterId": S(W1),
        "waiterName": S("Nino K."),
        "waiterEmail": S("nino.k@example.com"),
        "workingHours": N(40.5),
        "totalOrders": N(52),
        "avgServiceFeedback": N(4.6),
        "minServiceFeedback": N(4),
        "deltaAvgServiceFeedback": N(0.2),
        "deltaTotalOrders": N(4),
    }),
    put({
        "id": S("68b5f9f5-a2c9-417e-a12c-f97078d16321"),
        "reportType": N(0),
        "startDate": S("2026-10-20"),
        "endDate": S("2026-10-26"),
        "userId": S(W1),
        "locationId": S(L1),
        "locationName": S("9 Abashidze Street"),
        "waiterId": S(W1),
        "waiterName": S("Nino K."),
        "waiterEmail": S("nino.k@example.com"),
        "workingHours": N(23.5),
        "totalOrders": N(31),
        "avgServiceFeedback": N(4.6),
        "minServiceFeedback": N(4),
        "deltaAvgServiceFeedback": N(0.0),
        "deltaTotalOrders": N(-21),
    }),
    put({
        "id": S("aa38d9e0-601e-47c7-8524-c2b0e1876934"),
        "reportType": N(0),
        "startDate": S("2026-10-20"),
        "endDate": S("2026-10-26"),
        "userId": S(W2),
        "locationId": S(L2),
        "locationName": S("14 Baratashvili Street"),
        "waiterId": S(W2),
        "waiterName": S("Giorgi M."),
        "waiterEmail": S("giorgi.m@example.com"),
        "workingHours": N(24.5),
        "totalOrders": N(38),
        "avgServiceFeedback": N(4.6),
        "minServiceFeedback": N(3),
        "deltaAvgServiceFeedback": N(0.1),
        "deltaTotalOrders": N(3),
    }),
    put({
        "id": S("8bbd528e-cd50-4997-a199-eba30ec0884d"),
        "reportType": N(0),
        "startDate": S("2026-10-20"),
        "endDate": S("2026-10-26"),
        "userId": S(W3),
        "locationId": S(L3),
        "locationName": S("48 Rustaveli Avenue"),
        "waiterId": S(W3),
        "waiterName": S("Tamar B."),
        "waiterEmail": S("tamar.b@example.com"),
        "workingHours": N(12.5),
        "totalOrders": N(13),
        "avgServiceFeedback": N(4.4),
        "minServiceFeedback": N(3),
        "deltaAvgServiceFeedback": N(0.2),
        "deltaTotalOrders": N(1),
    }),
    put({
        "id": S("d20562e5-63bb-4514-bed8-8da06d1df754"),
        "reportType": N(0),
        "startDate": S("2026-10-20"),
        "endDate": S("2026-10-26"),
        "userId": S(W4),
        "locationId": S(L4),
        "locationName": S("22 Chavchavadze Avenue"),
        "waiterId": S(W4),
        "waiterName": S("Levan D."),
        "waiterEmail": S("levan.d@example.com"),
        "workingHours": N(16.0),
        "totalOrders": N(24),
        "avgServiceFeedback": N(4.9),
        "minServiceFeedback": N(5),
        "deltaAvgServiceFeedback": N(0.3),
        "deltaTotalOrders": N(5),
    }),
    put({
        "id": S("7df87aa5-591a-4a5f-bb57-6e1285c93cd0"),
        "reportType": N(0),
        "startDate": S("2026-10-20"),
        "endDate": S("2026-10-26"),
        "userId": S(W5),
        "locationId": S(L1),
        "locationName": S("9 Abashidze Street"),
        "waiterId": S(W5),
        "waiterName": S("Ana S."),
        "waiterEmail": S("ana.s@example.com"),
        "workingHours": N(13.5),
        "totalOrders": N(15),
        "avgServiceFeedback": N(4.6),
        "minServiceFeedback": N(4),
        "deltaAvgServiceFeedback": N(0.1),
        "deltaTotalOrders": N(2),
    }),
    put({
        "id": S("67bc0c38-7308-496b-9be6-0b0f70d2a7b0"),
        "reportType": N(1),
        "startDate": S("2026-10-13"),
        "endDate": S("2026-10-19"),
        "userId": S(W1),
        "locationId": S(L1),
        "locationName": S("9 Abashidze Street"),
        "totalOrders": N(48),
        "avgCuisineFeedback": N(4.5),
        "minCuisineFeedback": N(3),
        "revenue": N("4120.00"),
        "deltaAvgCuisineFeedback": N(0.1),
        "deltaRevenue": N("210.00"),
    }),
    put({
        "id": S("ca53c9bb-1873-45ad-b938-6dc6d8a2bc3f"),
        "reportType": N(1),
        "startDate": S("2026-10-20"),
        "endDate": S("2026-10-26"),
        "userId": S(W1),
        "locationId": S(L1),
        "locationName": S("9 Abashidze Street"),
        "totalOrders": N(31),
        "avgCuisineFeedback": N(4.7),
        "minCuisineFeedback": N(4),
        "revenue": N("3325.75"),
        "deltaAvgCuisineFeedback": N(0.2),
        "deltaRevenue": N("-794.25"),
    }),
    put({
        "id": S("8bfc8eaf-2b9e-4851-addf-7afc0f172f81"),
        "reportType": N(1),
        "startDate": S("2026-10-20"),
        "endDate": S("2026-10-26"),
        "userId": S(W2),
        "locationId": S(L2),
        "locationName": S("14 Baratashvili Street"),
        "totalOrders": N(38),
        "avgCuisineFeedback": N(4.7),
        "minCuisineFeedback": N(3),
        "revenue": N("4368.75"),
        "deltaAvgCuisineFeedback": N(0.0),
        "deltaRevenue": N("125.50"),
    }),
    put({
        "id": S("68541632-d8f5-4dec-b3cd-f7926646f970"),
        "reportType": N(1),
        "startDate": S("2026-10-20"),
        "endDate": S("2026-10-26"),
        "userId": S(W3),
        "locationId": S(L3),
        "locationName": S("48 Rustaveli Avenue"),
        "totalOrders": N(13),
        "avgCuisineFeedback": N(4.25),
        "minCuisineFeedback": N(3),
        "revenue": N("1360.50"),
        "deltaAvgCuisineFeedback": N("-0.1"),
        "deltaRevenue": N("80.00"),
    }),
    put({
        "id": S("68feb10e-1d4c-4cea-9a35-b8d40bb696db"),
        "reportType": N(1),
        "startDate": S("2026-10-20"),
        "endDate": S("2026-10-26"),
        "userId": S(W4),
        "locationId": S(L4),
        "locationName": S("22 Chavchavadze Avenue"),
        "totalOrders": N(16),
        "avgCuisineFeedback": N(4.8),
        "minCuisineFeedback": N(4),
        "revenue": N("1880.00"),
        "deltaAvgCuisineFeedback": N(0.4),
        "deltaRevenue": N("310.00"),
    }),
    put({
        "id": S("724ce06d-569c-4f9f-a0fd-56f3e9d9372a"),
        "reportType": N(1),
        "startDate": S("2026-10-20"),
        "endDate": S("2026-10-26"),
        "userId": S(W7),
        "locationId": S(L6),
        "locationName": S("17 Aghmashenebeli Avenue"),
        "totalOrders": N(7),
        "avgCuisineFeedback": N(4.5),
        "minCuisineFeedback": N(4),
        "revenue": N("760.25"),
        "deltaAvgCuisineFeedback": N(0.2),
        "deltaRevenue": N("40.00"),
    }),
]

dump("reportsHistory.json", "ReportsHistory", history)
