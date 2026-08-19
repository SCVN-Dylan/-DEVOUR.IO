using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// MOT MOC LEN CAP - khai bao DUY NHAT o day, ca scale lan camera lan gate an item deu doc chung.
/// Truoc kia moc nam o 2 noi (SimpleSuction.scaleSteps + CameraLevelZoom.steps) nen doi mot moc
/// phai sua 2 cho, quen la hai ben lech nhau.
///
/// Truoc day class nay long trong SimpleSuction. Da dua ra ngoai vi 'levelSteps' gio song trong
/// SuctionConfig - de nguyen trong SimpleSuction thi ScriptableObject phai tham chieu nguoc lai
/// mot MonoBehaviour chi de lay mot kieu du lieu, vong phu thuoc khong can thiet.
/// </summary>
[System.Serializable]
public class LevelStep
{
    [Tooltip("Level dat toi")]
    public int level = 10;

    [Tooltip("CONG THEM bao nhieu vao he so SCALE khi dat level nay (cong don).\n0 = moc nay khong lam to them")]
    public float add = 0.5f;

    [Tooltip("CONG THEM bao nhieu vao CO KHUNG CAMERA khi dat level nay (cong don).\n" +
             "Don vi = WORLD, chinh la orthographicSize - go 2 la khung rong them 2 don vi.\n" +
             "Khong con quy doi qua do FOV nhu ban cu.\n" +
             "0 = moc nay khong dong toi camera")]
    public float zoomAdd = 0f;

    [Tooltip("CONG THEM bao nhieu vao TAM HUT khi dat level nay (cong don), don vi world.\n" +
             "Cong THEM tren phan tang deu theo rangePerLevel, khong thay the.\n" +
             "0 = moc nay khong lam dai non hut (mac dinh hien tai)")]
    public float rangeAdd = 0f;

    [Tooltip("Moc nay co TIEN HOA khong (doi hinh dang player).\n" +
             "Bat len thi PlayerVisual se chuyen sang dang ke tiep khi qua moc nay.\n" +
             "Theo thiet ke: bat o stage 2 / 4 / 6.")]
    public bool isEvolution = false;
}

/// <summary>
/// BANG CAN BANG dung chung cho moi sinh vat co SimpleSuction (player lan bot).
///
/// VI SAO TACH RA ASSET: truoc day moi thong so nam thang tren component, tuc moi con mot ban
/// sao rieng. Doi mot con so can bang phai sua Player.prefab roi cau mong khong con nao trong
/// scene giu override cu - da tung lech that (moc Lv500 chi co tren instance, prefab khong co).
/// Gio mot asset, moi con tro toi, doi mot cho la ca van theo.
///
/// KHONG CHUA TRANG THAI RUNTIME. Cu the la KHONG chua 'level': level = 1 + tong XP, bi ghi lai
/// moi mieng an. Nhet vao day thi ca player lan 8 bot dung chung mot level, va moi lan an la ghi
/// thang vao file asset (trong Editor thi luu xuong dia that, choi thu vai phut la asset hong).
/// Level o lai SimpleSuction, dang private - xem ghi chu ben do.
///
/// MUON BOT KHAC PLAYER: tao asset thu hai roi keo vao prefab bot. Khong phai sua mot dong code.
///
/// LUU Y KHI TUNE: sua gia tri tren asset LUC DANG PLAY se GIU LAI sau khi thoat Play (khac
/// component tren GameObject - cai do tu reset). Tien de tune, nhung de vo tinh doi vinh vien.
/// </summary>
[CreateAssetMenu(fileName = "SuctionConfig", menuName = "Devour/Suction Config", order = 0)]
public class SuctionConfig : ScriptableObject
{
    [Header("An khi cham than")]
    [Tooltip("Item cham vao than nhan vat la nuot luon - NHUNG VAN PHAI DAT HANG.\n" +
             "Item qua hang thi cham vao khong an duoc (PhysicsDevourable tu bat va cham roi goi EatByContact).")]
    public bool eatOnContact = true;

    [Header("Cap do")]
    [Tooltip("BAT: an theo MA TRAN HANG. So sanh HANG cua item voi STAGE cua player (deu suy ra tu\n" +
             "levelSteps), khong phai so sanh level tho:\n" +
             "  hang - stage <= 0 : an duoc\n" +
             "  hang - stage == 1 : giay tai cho lam moi\n" +
             "  hang - stage >= 2 : bat dong\n" +
             "TAT: an tuot, khong khoa gi.")]
    public bool useLevelGate = true;

    [Header("Len cap thi to len")]
    [Range(0f, 100f)]
    [Tooltip("Moi cap thuong nhan vat to them bao nhieu (0.015 = +1.5%/cap).\n" +
             "Level trung MOC trong levelSteps thi KHONG cong khoan nay, chi cong 'add' cua moc.")]
    public float scalePerLevel = 0.015f;

    [Tooltip("DANH SACH MOC DUY NHAT cho ca scale, camera va gate an item.\n" +
             "Giua 2 moc van to dan deu theo scalePerLevel, toi moc thi cong nguyen mot cuc.\n" +
             "Doi mot moc o day la ca ba thu tu theo, khong phai sua cho nao khac.\n\n" +
             "De TRONG thi khong co hang nao ngoai hang 1, va camera/scale chi tang deu.")]
    public List<LevelStep> levelSteps = new List<LevelStep>();

    [Tooltip("TRAN kich thuoc: to toi da = scale goc x so nay, du level bao nhieu cung khong vuot.\n" +
             "0 = khong gioi han (to mai theo cap)")]
    public float maxScale = 20f;

    [Range(0f, 2f)]
    [Tooltip("TOC DO DI CHUYEN bam theo co than: speed = speed goc x (1 + (he so scale - 1) x so nay).\n" +
             "Tu co san ca phan nhich moi mieng an lan cu nhay o moc, vi he so scale da co san.\n" +
             "  0    = toc do dung im -> cang to cang i\n" +
             "  0.25 = nang gap ~2.8 lan luc dau (mac dinh)\n" +
             "  1    = toc tang dung bang co -> to ma khong i ti nao, nhung map thanh be")]
    public float speedFollowScale = 0.25f;

    [Range(0f, 100f)]
    [Tooltip("Moi cap non hut dai ra bao nhieu (0.15 = +15%/cap)")]
    public float rangePerLevel = 0.15f;
}
