window.itemMetadata = {
  body: {
    name: "Body",
    type_name: "body",
    required: ["male", "female"],
    animations: ["idle"],
    variants: ["light"],
    layers: {
      layer_1: {
        zPos: 0,
        male: "body/male"
      }
    }
  },
  hat_blue: {
    name: "Blue Hat",
    type_name: "hat",
    required: ["male"],
    animations: ["idle"],
    variants: ["blue", "red"],
    layers: {
      layer_20: {
        zPos: 20,
        male: "head/hat"
      }
    }
  }
};

window.categoryTree = {
  items: ["body"],
  children: {
    hats: {
      required: ["male"],
      items: ["hat_blue"],
      children: {}
    }
  }
};
