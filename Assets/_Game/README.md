# Sticky Peer Sort prototype

## Scene mẫu theo reference

Bốn mẫu hiện dùng **32 / 40 / 48 / 64 sticker**, tương ứng **8 / 10 / 12 / 16 bộ**, xếp chồng ngẫu nhiên và trộn category. Trong asset level mẫu, chỉnh **Generated Sticker Count** (bội số 4) và **Layout Seed**, rồi chạy **Tools > Sticker Category Game > Fill Current Scene Trays with Dense Sticker Piles** để tạo lại. Menu này thay nội dung mẫu và kiểm tra đường thắng trước khi lưu; các level chính được giữ riêng.

Mở `Assets/_Game/Scenes/SampleStickerLevel.unity` để xem bốn bố cục **Grid4 / Rows2 / Mixed3 / Pile1**. Menu **Tools > Sticker Category Game > Reference Style > Generate Sample Scene and Four Levels** tạo lại view/prefab; **Level Preview** chọn từng mẫu để Play. Scene chính, level hiện có và tiến trình chính được giữ riêng. Tài liệu phân tích, cấu hình và ảnh so sánh nằm ở `Docs/ReferenceStyle/ArtStyleAnalysis.md` và `Docs/ReferenceStyle/LevelLayoutNotes.md`.

## Chạy game

Hiệu ứng dán sticker: bóc cong mép tại chỗ → nhảy đến ô → trải phẳng theo chiều ngược lại. Cấu hình **Page Peel / Peel Duration / Stick Duration** trong Balance; xem `Docs/StickerPeel.md`. Cả scene chính và scene reference đã bật hiệu ứng.

Mở `Assets/_Game/Scenes/StickerCategoryPrototype.unity`, nhấn **Play**. Game khởi đầu Level 1; nút **bánh răng > Continue Saved** mở level đã lưu. Nhấn sticker để thu vào category đang mở; sticker của category tương lai vào khay. Khay chờ đầy ô sẽ thua sau khi xử lý hết các chuyển tự động. Thu đủ mọi mục tiêu và hết khay để thắng.

**Undo** hoàn tác một lượt, kể cả trạng thái ice/buffer/category. **Hint** dùng lời giải được bộ giải xác minh. **+ Slot** tăng một ô trong level hiện tại. **Mix** chỉ áp dụng khi bộ giải xác minh được cách hoàn thành bố cục mới. Các level mẫu không cần booster. Lưu thưởng một lần cho mỗi level, không nhận lại coin khi chơi lại.

Chỉ chọn được sticker **không bị sticker nào khác đè lên**, kể cả khi phần bên cạnh còn lộ. Bấm sticker bị đè sẽ rung nhẹ và nháy đỏ, không đổi trạng thái puzzle. Lấy sticker bên trên ra thì sticker dưới mới mở. Hint, solver và Linked dùng cùng quy tắc này.

Sticker bị đè có lớp phủ đen với mép mềm theo alpha artwork để nhận biết trạng thái bị chặn. Lớp phủ nằm dưới chữ và icon khóa, không nhận raycast, không sửa texture gốc và không đổi geometry chọn sticker. Tự bỏ lớp phủ ngay khi không còn bị đè; Undo/Restart/Shuffle dựng lại đúng trạng thái. Chỉnh **Covered Opacity** và **Covered Softness** trong `UI/LayerLabTheme.asset`. Softness làm mềm mép lớp tối, không làm nhòe artwork gốc.

**Logic thua:** sau khi xử lý xong các lượt tự chuyển vào category, nếu số sticker chờ **bằng hoặc lớn hơn** dung lượng khay thì thua (4/4 hoặc 5/5 tùy level). Không cần lấy thêm sticker vượt khay. Lượt bấm tiếp bị khóa ngay; sticker cuối vẫn bay và dán xong, giữ trạng thái đầy 0.24 giây rồi mở **TRAY FULL!** với Retry/Undo. Thua không cộng coin. Undo trả lại trước lượt làm đầy khay; Retry tạo lại level. Cả 14 level đang bật **Fail When Buffer Is Full**.

Sticker trên board của bốn mẫu reference dùng **Board Sticker Size Multiplier = 0.75**: giảm 25% cả hình và alpha hitbox; category/ô chờ giữ nguyên kích thước. Giá trị này được giữ khi tạo lại bố cục mẫu. Level 3 chính đã có trace mới theo luật thua khi đầy; không đổi vị trí các sticker của level chính.

Để xem thua trên scene chính mà không phải chơi qua hai màn đầu: nhấn **Play**, chọn **Tools > Sticker Category Game > Preview Lose - Level 3 (Play Mode)**. Công cụ chạy chuỗi chọn hợp lệ trên layout Level 3 thật và dừng khi khay đầy 4/4; không thay level asset hoặc ép state Fail. Đợi tự chạy xong rồi thử Undo/Retry; tránh bấm xen giữa lúc preview chạy. Menu này chỉ bật khi scene đang chơi có Level 3.

Project dùng **Old Input Manager** (`activeInputHandler: 0`) và **StandaloneInputModule**. Sticker xử lý **PointerDown** để phản hồi ngay lúc nhấn. Các lượt thu bình thường cho phép nhiều sticker cùng bay; kết quả vào ô được commit theo thứ tự bấm. Chỉ tạm khóa thao tác khi hoàn tất/thay khay hoặc kết thúc level để giữ đúng chuỗi animation. Booster đợi các chuyến bay xong trước khi thay đổi bàn chơi. **Click Fail Duration / Distance** trong Balance chỉnh phản hồi bấm sai.

Sau khi đổi **Active Input Handling**, cần **khởi động lại toàn bộ Unity Editor**. Chỉ recompile hoặc bật/tắt Play Mode có thể khiến API Input không báo lỗi nhưng không nhận thiết bị (`Input.mousePresent == false`). Nếu package New Input còn hiện hộp thoại đề nghị bật backend mới, chọn **Don't Enable** để giữ Old Input theo cấu hình project. Test PlayMode kiểm tra cả việc đăng ký chuột thật trên Windows Editor, không chỉ việc gọi API không ném exception.

## Placeholder và TextMeshPro

Toàn bộ chữ dùng **TextMeshProUGUI**, giao diện Layer Lab dùng **Sen SDF** có sẵn trong pack. Placeholder dùng bản sao các hình tròn/vuông có sẵn trong DemiLib; không cần tải artwork. Mỗi sticker có màu, tên và category riêng.

Để thêm artwork: chọn một asset trong `Assets/_Game/Data/Stickers`, kéo PNG có alpha vào **Texture** hoặc gán **Sprite**. Texture có ưu tiên nếu cả hai được gán. Nhấn **Use production artwork colors / hide placeholder label** để giữ màu gốc, ẩn nhãn và giữ tỉ lệ ảnh. Inspector tự bake alpha mask khi thay cấu hình; **Read/Write có thể tắt** trên texture production. Không cần hình vuông/tròn hoặc thay gameplay: đường viền, lỗ và vùng lõm được lấy từ alpha ảnh. Chỉnh **Size / Scale** trên placement để thay kích thước; **Preserve Aspect** ngăn kéo méo ảnh. Xem [ARCHITECTURE.md](ARCHITECTURE.md) để biết cách dùng và các giới hạn sampling.

## UI Layer Lab

Scene đã dùng sprite và font từ `Assets/Layer Lab/GUI Pro-SuperCasual`: nền vàng gỗ ấm, thẻ giấy kem có viền/ribbon màu category đậm, tiến độ collection, khay chờ có viền nổi, các khay sticker màu gỗ có bố cục khác nhau theo level và các nút booster màu rực có icon/count riêng. Bảng màu lấy cảm hứng từ ảnh store của Sticker Jam: Category Sort (com.RimalGames.StickerJam); toàn bộ sprite/font vẫn dùng asset có trong project. Mỗi ô mục tiêu hiển thị bản mờ của chính artwork trong Exact Targets; sticker bay vào đúng ô tương ứng rồi mới hiện đủ màu. Nhãn tên chỉ dành cho placeholder, tự ẩn theo Show Label khi dùng artwork thật. Nút có phản hồi nhấn; popup pause/restart/win/fail dùng cùng theme. Layout theo 1080×1920, scale vừa safe area; thẻ một category dùng một hàng bốn ô, nhiều category dùng lưới hai cột. Scene lưu sẵn preview Level 1 để xem trong Editor; Play dựng lại từ model thật.

Đổi font, màu hoặc sprite tại `Assets/_Game/UI/LayerLabTheme.asset`, sau đó chạy **Tools > Sticker Category Game > Redesign UI with Layer Lab** để cập nhật scene và prefab. Lệnh giữ theme đã chỉnh, level, balance và artwork sticker; tạo một bản sao scene trước lần áp dụng đầu tại `Scenes/Backups/StickerCategoryPrototype_BeforeLayerLab.unity`. **Build Prototype** cũng tự áp dụng giao diện này khi pack có trong project. Lệnh dựng lại UI thay thế hierarchy UI hiện tại, nên lưu các chỉnh sửa tay trước khi chạy.

**Tools > Sticker Category Game > Apply Warm Arcade Palette** đặt lại bảng màu vàng gỗ/kem và dựng lại UI. `UIWoodGrain` vẽ vân gỗ bằng mesh UI tĩnh, không cần tải thêm texture. Bản scene pastel trước lần đổi này được giữ tại `Scenes/Backups/StickerCategoryPrototype_PastelUI.unity`.

Mã dựng giao diện nằm ở `Editor/LayerLabUIBuilder.cs`; theme chỉ thuộc Presentation, không thêm phụ thuộc UI vào Gameplay/Core. Asset gốc của Layer Lab và giấy phép font OFL trong pack được giữ nguyên. PNG sticker có thể thay riêng như hướng dẫn artwork phía trên.

Chữ dùng ba material preset TMP SDF tại `UI/TextMaterials`: **Sen Warm Heading** (chữ trắng lớn, viền nâu đậm và bóng lệch xuống), **Sen Warm Light** (chữ trắng nhỏ, viền mảnh), **Sen Warm Dark** (chữ nâu, viền kem và bóng nhẹ). Chỉnh Outline/Underlay trực tiếp trên material trong Inspector; theme giữ reference riêng cho từng preset. Các chữ sinh lúc chạy, nhãn sticker, ô chờ, ô đích và animation đều dùng preset chung. Không sửa material Sen gốc, không tạo bản material riêng cho từng chữ; bật Extra Padding để viền/bóng không bị cắt sát glyph.

## Dữ liệu và kiến trúc

- `Runtime/Data`: ScriptableObject category, sticker, level, balance. Source assets được sao sang runtime khi vào level.
- `Runtime/Core`: session, state machine, deterministic move resolution, buffer, special handlers, geometry, bounded solver và validator.
- `Runtime/Gameplay`: coordinator không phụ thuộc UI, booster service và dependency ports cho level/session/solver/presenter/save/screen. Có assembly riêng, không tham chiếu UGUI/TMP.
- `Runtime/Presentation`: UI TMP, alpha raycast, các chuyến bay/pop/completion và phản hồi sticker.
- `Runtime/Services`: lưu PlayerPrefs với JSON version 1; interface sound/haptic với fallback không âm thanh.
- `Editor`: bootstrap và custom inspector validate level.
- `Prefabs`: Sticker, CategoryCard, BufferSlot. Scene có canvas, safe area, camera, light, StandaloneInputModule và reference đã nối.

`GameController` hiện chỉ nối dependency và chuyển tiếp API của scene. `GameplayCoordinator` điều phối flow, `GameplayBoosters` xử lý inventory, `MovePresentationPlayer` chạy animation, còn view chỉ đọc `IGameplayReadModel` và gửi lệnh qua `IStickerInput`. Tách biệt được kiểm tra bằng assembly references và test coordinator chạy với screen/presenter giả, không có Canvas. Chi tiết SOLID và sơ đồ phụ thuộc ở [ARCHITECTURE.md](ARCHITECTURE.md).

**Create > Sticker Sort** tạo Category / Sticker / Level / Balance. Tạo category trước, gán category vào sticker, thêm objective theo thứ tự xuất hiện, rồi thêm placement với ID duy nhất, definition, position, rotation, size, scale, sorting order và pile ID. Board tọa độ local khoảng x ±430, y ±370, trên thiết kế 1080×1920. Sorting order lớn hơn nằm trên. Mỗi sticker phải ứng với một target. Điền **Exact Targets** theo thứ tự ô mong muốn trên thẻ: cùng definition với placement trên board, đủ Target Count phần tử. Tất cả 10 level mẫu đã có cấu hình này. Để rỗng vẫn hỗ trợ luật cũ “bất kỳ sticker nào trong category”, nhưng sẽ không có artwork cố định cho từng ô.

Special: **Mystery** hiện “?” và che artwork khi còn bị đè, tự lộ khi không còn sticker phía trên (không cần bấm riêng để lộ); Undo khôi phục trạng thái ẩn. Level 9–10 có Mystery. **Locked** ở các level mẫu 6–10 dùng CategoryCompleted: phải thu đủ cả bộ có icon chìa khóa trên tiêu đề mới mở khóa, thu một sticker riêng lẻ chưa đủ. Biểu tượng ổ khóa và nhãn CLEAR KEY SET hiển thị trên sticker bị khóa.

Các tùy chọn khóa cũ vẫn hỗ trợ cho level tự thiết kế: **Locked** chọn StickerRemoved / CategoryCompleted / KeyCollected và ID phụ thuộc; **Frozen** đặt số lớp; **Linked** dùng cùng link ID cho đúng hai sticker; **Mystery** giữ definition thật trong dữ liệu. Ice về 0 thì lượt tiếp theo thu sticker. Linked yêu cầu hai sticker đều không bị đè, không tự thu sticker còn lại.

Chọn level và nhấn **Validate and generate solution trace**, hoặc **Tools > Sticker Category Game > Validate All Levels**. Báo cáo tại `Validation/LevelSolutions.txt`, trace có trong mỗi asset. Bộ giải trả riêng Solved / NoSolution / BudgetExceeded; hết ngân sách không có nghĩa level vô nghiệm. Giao nhau alpha giữa các sprite được lấy mẫu trong vùng giao nhau và cache khi load/shuffle; hình mới có chi tiết nhỏ hơn khoảng lấy mẫu 4 đơn vị board cần được kiểm tra lại. Chọn bằng chuột/touch, Hint và solver cùng dùng cache chặn này.

**Tools > Sticker Category Game > Build Prototype** dựng lại scene và prefab, bổ sung data còn thiếu; giữ level đã tạo và sprite đã thay. Lưu scene đang làm trước khi chạy vì lệnh mở scene prototype mới. Balance chứa duration, easing, inventory khởi tạo và bật/tắt booster. Save cũ giữ inventory đã lưu.

## Ô artwork cố định và bố cục khay

**Tools > Sticker Category Game > Apply Exact Sticker Targets and Varied Trays** cấu hình lại Exact Targets và bố cục mẫu của cả 10 level; giữ balance, artwork definition và cơ chế đặc biệt. Lệnh xác minh toàn bộ bản sao trước khi lưu, tạo lại trace và báo cáo `Validation/LevelTuning.md`. Bản trước khi đổi được giữ ở `UserSettings/StickyPeerSortBeforeVariedTrays`. Lệnh ghi đè target/layout mẫu; lưu thiết kế riêng trước khi chạy.

Ví dụ: LEMON ở ô 3 thì bấm LEMON đầu tiên vẫn bay đến ô 3; ô APPLE còn mờ. Preview, ảnh đang bay và ảnh sau khi đáp dùng chung sprite/texture và Preserve Aspect. Chuyến bay theo vị trí hiện tại của ô đích, kể cả khi khay chờ phải dàn lại do nhiều lượt bấm nhanh.

## Animation khay và các chồng sticker

Giao diện phát từng event theo thứ tự riêng, không đọc trạng thái cuối của model để vẽ trước kết quả. Sticker đến đúng ô đích thì mới hiện sprite và tăng counter; ô cuối được giữ đủ lâu để nhìn thấy 4/4, khay pulse rồi bay lên/mờ đi. Khay tiếp theo trượt vào và hiện rõ ở đúng vị trí khay vừa rời. Chỉ sau đó sticker trong ô chờ mới bắt đầu bay từ đúng ô của nó sang khay mới. Các khay khác giữ nguyên vị trí trong suốt lượt, kể cả khi có nhiều lần hoàn tất liên tiếp.

Trong `Data/Balance.asset`, chỉnh **Category Filled Hold**, **Category Duration**, **Category Exit Duration** và **Category Enter Duration** để điều chỉnh nhịp animation. `GameplayUI.categoryViews` lưu các target đang hiển thị; `CategoryCardPresentation` cập nhật từng ô ngay khi sticker đáp xuống.

**Tools > Sticker Category Game > Bake All Sticker Artwork** tạo lại alpha mask cho tất cả sticker. Reimport ảnh sẽ cập nhật mask cho những sticker đang dùng ảnh đó. Sau khi thay artwork, validate lại level vì hình dạng mới có thể thay đổi sticker nào đang đè lên sticker khác.

Các level mẫu hiện có **1–6 Piles** với vị trí, kích thước và góc khác nhau: ba khay lệch, bốn ngăn 2×2, hai khay dài, ba hàng ngang, một khay lớn, một khay dài kèm bốn khay nhỏ, sáu ngăn và khay dọc kèm ba ngăn bên cạnh. Sticker được giới hạn trong lòng khay theo cả góc xoay. Piles vẫn là dữ liệu trên LevelDefinition, không phải layout hardcode lúc chơi. Sticker được xoay và xếp chồng trong các khay ngay từ Level 1. **Tools > Sticker Category Game > Apply Overlapping Sample Piles** áp lại bố cục mẫu cho 10 level và xác minh lời giải; lệnh này ghi đè transform/pile của các level mẫu. Có thể sửa từng pile và placement trực tiếp trong Inspector để tự thiết kế bố cục.

## Game feel, độ khó và âm thanh

Nhịp mặc định: sticker bay **0.24 giây**, giữ ô cuối **0.20 giây**, pulse khay **0.24 giây**, khay cũ ra **0.20 giây**, khay mới vào **0.24 giây**. Bóp nhẹ lúc nhấn, nâng/bóc và xoay về ngay ngắn được gộp trong chuyến bay; các lần bấm thường vẫn bay đồng thời. Âm thanh đặt phát sau khi sticker hiện ở ô đích. `Balance.asset` có Flight Arc, Flight Pop, Celebrations cùng các duration để tự chỉnh. Hiệu ứng phụ có coroutine/layer riêng và không khóa input; restart/đổi level hủy hiệu ứng cũ.

Đường cong level: **1–2** làm quen; **3–4** quản lý sticker của bộ chưa mở; **5** thêm bộ mục tiêu thứ năm; **6** khóa mở bằng cách hoàn tất bộ có chìa khóa; **7** băng; **8** liên kết; **9** bí ẩn; **10** tổng hợp với bốn sticker tương lai đè phía trên. Các cơ chế đã học vẫn xuất hiện ở màn sau. Tất cả giữ khay chờ bốn ô và có lời giải không dùng booster. Xem `Validation/LevelTuning.md` để xem số hành động, số sticker chờ và kết quả 128 lượt mô phỏng ngẫu nhiên mỗi màn; đây là số liệu chẩn đoán, cần chơi thử để đánh giá độ khó thực tế.

`Data/Audio.asset` chứa volume và 10 clip WAV tổng hợp dùng ngay cho prototype; có thể thay từng clip sau. Popup có nút **SOUND: ON/OFF**, lưu lựa chọn riêng. Hoàn tất bộ có tia sáng; thắng màn có confetti, và chỉ lần đầu nhận thưởng mới có dòng coin/coin bay vào HUD. Coin vẫn do save service cấp đúng một lần; animation không cộng coin. Chưa thêm nhạc nền.

**Tools > Sticker Category Game > Apply Game Feel and Level Curve** áp lại cấu hình và layout mẫu, ghi đè các level mẫu hiện tại. Bản gốc trước lần chỉnh đầu được giữ ngoài Assets tại `UserSettings/StickyPeerSortBeforeFeelTuning`. Chạy Validate All Levels sau khi thay hình dạng artwork hoặc tự chỉnh layout.

## Kiểm tra và giới hạn prototype

Unity Test Runner: chạy `StickyPeerSort.Tests` (EditMode) và `StickyPeerSort.PlayTests` (PlayMode). Test kiểm tra model, alpha priority, specials, undo/restart, save, solver và cả 10 trace. PlayMode kiểm tra raycast thật qua EventSystem, chống double tap, chơi hết 10 level qua controller/animation, Next, Retry, Fail và Undo; test bổ sung kiểm tra ô cuối hiện 4/4 trước khi khay rời, khay mới vào xong mới nhận sticker chờ, cascading completion, ba khay và thứ tự raycast tại vùng sticker bị đè.

Luật category replacement, overflow sau resolution, ice, linked, mystery là các mặc định lấy từ prompt, chưa được xác nhận bằng gameplay trực tiếp của game tham khảo. UI dùng tiếng Anh theo font có sẵn. Đã có SFX tổng hợp; rung Android là tùy chọn trong Balance, chưa thử trên thiết bị. Album hiện lưu số level hoàn thành và thưởng, chưa có giao diện album riêng. Chưa build APK hoặc đo FPS trên Android; cần artwork, localization/font tiếng Việt, device profiling và chơi thử độ khó trước production. Ô category được cập nhật ngay khi sticker đáp xuống; chỉ các ô chờ được dồn lại cuối lượt. Âm thanh dùng pool sáu AudioSource; hiệu ứng UI chưa pooling vì chưa có số liệu profiling.


## Editor chọn level

Mở **Tools > Sticker Category Game > Level Preview**. Chọn level trong danh sách, nhấn **Preview Selected in Game View** để xem khi chưa Play; dùng **Play Selected** để vào chơi trực tiếp màn đã chọn, kể cả sau domain reload. **Select Level Asset** chọn cấu hình để sửa. Preview ở Edit Mode dùng progress tạm, không tự lưu scene hoặc PlayerPrefs. Khi Play, đây là game thật và có lưu tiến trình theo luật bình thường.

Mystery ở Level 9–10 được xếp lệch dưới một sticker để phần có dấu “?” nhìn thấy ngay. Sticker khóa dùng kích thước thường, mặt khóa trung tính thay cho artwork và icon lớn; khi mở sẽ hiện lại đúng artwork.

Hoàn tất bộ có chìa khóa: ô cuối đáp xuống → giữ đủ số lượng và pulse → chìa khóa bay từ header đến khóa → icon khóa xoay, nâng và mờ đi → mặt khóa lật thành artwork → khay cũ rời đi và khay mới vào. Mystery tự lật khi được bỏ lớp che, đổi artwork ở giữa cú lật. Trong lúc mở khóa/lật, input tạm khóa để tránh lấy sticker trước khi hiệu ứng hoàn tất; các lượt bay thông thường vẫn nhận bấm liên tiếp.

Chỉnh **Key Flight Duration** (0.45s), **Lock Open Duration** (0.28s), **Special Reveal Duration** (0.32s) trong `Assets/_Game/Data/Balance.asset`. Không cần Animator hoặc asset mới. Đổi level hủy hiệu ứng đang chạy; Undo sau lượt trả lại mặt khóa/bí ẩn theo snapshot.

Per-level content controls and live Editor preview: see Docs/LevelAuthoring.md.

Level analysis, solution replay and batch validation: see Docs/LevelAnalysis.md.

Interactive drag peeling and most-filled matching sheet priority: see Docs/InteractivePeel.md.
